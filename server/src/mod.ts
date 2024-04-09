//import modConfig from "../config/config.json";

import fs from "fs";
import chokidar from "chokidar";
import jszip from "jszip";


import { DependencyContainer } from "tsyringe";
import type { IPreAkiLoadMod } from "@spt-aki/models/external/IPreAkiLoadMod";
import type { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import type { IPostAkiLoadMod } from "@spt-aki/models/external/IPostAkiLoadMod";
import { HttpListenerModService } from "@spt-aki/services/mod/httpListener/HttpListenerModService";

import { ConfigServer } from "@spt-aki/servers/ConfigServer";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import { VFS } from "@spt-aki/utils/VFS";
import { HttpResponseUtil } from "@spt-aki/utils/HttpResponseUtil";
import { HashUtil } from "@spt-aki/utils/HashUtil";
import path from "path";
import { IncomingMessage, ServerResponse } from "http";


const modName = "RemotePlugins";
const bepinexFilePath = "BepInEx";
const expectedBepinexDirectories = ["config", "plugins"];
const storagePath = "files";


type File = {
    Name: string,
    Hash: string,
    Size: number
}

class FileMap {
    private files: File[] = [];

    public addFile(Name: string, Hash: string, Size: number) {
        this.files.push({ Name, Hash, Size });
    }

    public removeFile(name: string) {
        this.files.splice(this.files.findIndex(f => f.Name === name), 1);
    }

    public getFiles(): File[] {
        return this.files;
    }

    public hasFile(file: string): boolean {
        return this.files[file] !== undefined;
    }
}

class PluginFileMap {
    Files: File[];
    FilesHash: string;
    Zip: File;
}

enum Synchronization {
    UpdateOnly = "UPDATE_ONLY",  // Client should only update the files that are available in the FileMap and leave all other files alone
    DeleteAndSync = "DELETE_AND_SYNC" // Client should delete all files in the directories which have files in the FileMap
}

type ClientOptions = {
    SyncType: Synchronization
}

type ModConfig = {
    Debug: boolean;
    ClientOptions: ClientOptions;
}

class RemotePlugins implements IPreAkiLoadMod, IPostAkiLoadMod, IPostDBLoadMod {
    private logger: ILogger;
    private vfs: VFS;
    private hashUtil: HashUtil;

    private rootPluginPath: string;
    private storagePath: string;
    private normalizedStoragePath: string;
    private bepinexFilePath: string;
    private fileWatcher: chokidar.FSWatcher;
    private cachedPluginFileMap: PluginFileMap;
    private cachedPluginFileMapJson: string;
    private filesChanged: boolean;

    private modConfig: ModConfig;

    public preAkiLoad(container: DependencyContainer): void {
        this.logger = container.resolve<ILogger>("WinstonLogger");

        this.rootPluginPath = path.normalize(path.join(__dirname, "../"));
        this.LoadModConfig();
        const dirs = this.rootPluginPath.split(path.sep).join("/");

        this.storagePath = `${dirs}${storagePath}/`
        this.normalizedStoragePath = path.normalize(this.storagePath);
        this.bepinexFilePath = `${dirs}${bepinexFilePath}/`;

        this.LogInfo(`Root plugin path: ${this.rootPluginPath}`);
        this.LogInfo(`Storage path: ${this.storagePath}`);
        this.LogInfo(`BepInEx path: ${this.bepinexFilePath}`);

        const httpListenerService = container.resolve<HttpListenerModService>("HttpListenerModService");
        httpListenerService.registerHttpListener("RemotePluginsClientOptionsHttpListener",
            this.remotePluginsClientOptionsHttpListener_canHandleOverride, this.remotePluginsClientOptionsHttpListener_handleOverride());
        httpListenerService.registerHttpListener("RemotePluginsFileMapHttpListener",
            this.remotePluginsFileMapHttpListener_canHandleOverride, this.remotePluginsFileMapHttpListener_handleOverride());
        httpListenerService.registerHttpListener("RemotePluginsFileHttpListener",
            this.remotePluginsFileHttpListener_canHandleOverride, this.remotePluginsFileHttpListener_handleOverride());
    }

    private LoadModConfig() {
        this.modConfig = this.generateDefaultModConfig();
        try {
            if (!fs.existsSync(`${this.rootPluginPath}/config`)) {
                fs.mkdirSync(`${this.rootPluginPath}/config`);
            }
            if (fs.existsSync(`${this.rootPluginPath}/config/config.json`)) {
                const configReader = fs.readFileSync(`${this.rootPluginPath}/config/config.json`, "utf8");
                this.modConfig = { ...this.modConfig, ...JSON.parse(configReader) as ModConfig };
            }
            // this is to ensure that the config is always up to date
            fs.writeFileSync(`${this.rootPluginPath}/config/config.json`, JSON.stringify(this.modConfig, null, 4));
        } catch (e) {
            this.LogError(e.stack);
        }
    }


    public remotePluginsClientOptionsHttpListener_canHandleOverride(sessionId: string, req: IncomingMessage): boolean {
        return req.method === "GET" && req.url?.includes(`/${modName}/ClientOptions`);
    }

    public remotePluginsClientOptionsHttpListener_handleOverride(): (sessionId: string, req: IncomingMessage, resp: ServerResponse) => void {
        // biome-ignore lint/complexity/noUselessThisAlias: this is overriden by the httpListenerService so we need the alias
        const _this = this;
        const responseFunc = async (sessionId: string, req: IncomingMessage, resp: ServerResponse): Promise<void> => {
            const clientOptionsJson = JSON.stringify(_this.modConfig.ClientOptions);
            resp.setHeader("Content-Type", "application/json");
            resp.setHeader("Content-Length", clientOptionsJson.length);
            resp.writeHead(200, "OK");
            resp.end(clientOptionsJson);
        }
        return responseFunc;
    }


    public remotePluginsFileMapHttpListener_canHandleOverride(sessionId: string, req: IncomingMessage): boolean {
        return req.method === "GET" && req.url?.includes(`/${modName}/FileMap`);
    }

    public remotePluginsFileMapHttpListener_handleOverride(): (sessionId: string, req: IncomingMessage, resp: ServerResponse) => void {
        // biome-ignore lint/complexity/noUselessThisAlias: this is overriden by the httpListenerService so we need the alias
        const _this = this;
        const responseFunc = async (sessionId: string, req: IncomingMessage, resp: ServerResponse): Promise<void> => {
            let retryCount = 0;
            while (!_this.cachedPluginFileMapJson && retryCount < 60) { // 30 seconds
                await _this.sleep(500);
                retryCount++;
                _this.LogDebug(`Waiting for file map: ${retryCount}`);
            }
            if (!_this.cachedPluginFileMapJson && retryCount >= 40) {
                resp.writeHead(500, "Failed to build file map");
                resp.end();
                return;
            }

            resp.setHeader("Content-Type", "application/json");
            resp.setHeader("Content-Length", _this.cachedPluginFileMapJson.length);
            resp.writeHead(200, "OK");
            resp.end(_this.cachedPluginFileMapJson);
        }
        return responseFunc;
    }


    public remotePluginsFileHttpListener_canHandleOverride(sessionId: string, req: IncomingMessage): boolean {
        return req.method === "GET" && req.url?.includes(`/${modName}/File`);
    }

    public remotePluginsFileHttpListener_handleOverride(): (sessionId: string, req: IncomingMessage, resp: ServerResponse) => void {
        // biome-ignore lint/complexity/noUselessThisAlias: this is overriden by the httpListenerService so we need the alias
        const _this = this;
        const responseFunc = async (sessionId: string, req: IncomingMessage, resp: ServerResponse): Promise<void> => {
            let retryCount = 0;
            while (!_this.cachedPluginFileMap && retryCount < 60) { // 30 seconds
                await _this.sleep(500);
                retryCount++;
            }
            if (!_this.cachedPluginFileMap && retryCount >= 40) {
                resp.writeHead(500, "Failed to get file");
                resp.end();
                return;
            }
            if (!_this.cachedPluginFileMap.Zip) {
                resp.writeHead(404, "No file");
                resp.end();
                return;
            }
            if (_this.cachedPluginFileMap.Zip.Name !== "bepinex.zip" || !_this.cachedPluginFileMap.Zip.Hash || !_this.cachedPluginFileMap.Zip.Size) {
                resp.writeHead(500, "Failed to get file");
                resp.end();
                return;
            }

            resp.setHeader("Content-Type", "application/zip");
            resp.setHeader("Content-Length", _this.cachedPluginFileMap.Zip.Size);
            resp.writeHead(200, "OK");

            const readStream = fs.createReadStream(`${_this.storagePath}bepinex.zip`);
            readStream.pipe(resp);
        }
        return responseFunc;
    }

    public postDBLoad(container: DependencyContainer): void {
        this.vfs = container.resolve<VFS>("VFS");
        this.hashUtil = container.resolve<HashUtil>("HashUtil");
    }

    public async postAkiLoad(container: DependencyContainer): Promise<void> {
        if (!this.vfs.exists(this.storagePath)) {
            this.vfs.createDir(this.storagePath);
        }

        if (expectedBepinexDirectories.length > 0) {
            for (const dir of expectedBepinexDirectories) {
                const expectedPath = `${this.bepinexFilePath}${dir}/`;
                this.logger.info(`Checking for ${expectedPath}`);
                if (!this.vfs.exists(`${expectedPath}`)) {
                    this.logger.info(`Creating ${expectedPath}`);
                    this.vfs.createDir(`${expectedPath}`);
                }
            }
        } else {
            if (!this.vfs.exists(this.bepinexFilePath)) {
                this.vfs.createDir(this.bepinexFilePath);
            }
        }

        this.fileWatcher = chokidar.watch([this.bepinexFilePath, this.storagePath], {
            persistent: true,
            ignoreInitial: true,
            depth: 5,
        });

        this.fileWatcher
            .on("add", (path) => { this.onFileAdd(path) })
            .on("change", (path) => { this.onFileChange(path) })
            .on("unlink", (path) => { this.onFileUnlink(path) })
            .on("error", (error) => { this.LogError(error.stack) });

        // load the file map
        try {
            const fileMapJson = this.vfs.readFile(`${this.storagePath}fileMap.json`);
            const fileMap = JSON.parse(fileMapJson) as PluginFileMap;
            const filesDidChange = await this.FilesDidChange(fileMap);
            if (!filesDidChange) {
                this.cachedPluginFileMap = fileMap
                this.cachedPluginFileMapJson = fileMapJson;
            } else {
                this.LogInfo("fileMap.json changed");
            }
        } catch (e) {
            // no file map
            this.filesChanged = true;
            this.rebuildFiles();
            return;
        }

        // check if the bepinex files have changed
        try {
            if (this.cachedPluginFileMap.Zip) {
                const bepinexHash = this.generateFileHash(`${this.storagePath}bepinex.zip`);
                if (bepinexHash === this.cachedPluginFileMap.Zip.Hash) {
                    this.LogInfo("files are the same, no rebuild needed");
                    return;
                }
                this.LogInfo("bepinex.zip changed");
            }
        } catch (e) { }

        // no hash or hash is different
        this.filesChanged = true;
        this.rebuildFiles();
        return;
    }

    private onFileAdd(filePath: string) {
        if (filePath.startsWith(this.normalizedStoragePath)) return;
        this.filesChanged = true;
        this.LogInfo(`File added: ${filePath.replace(this.rootPluginPath, "")}`);
        this.rebuildFiles();
    }

    private onFileChange(filePath: string) {
        if (!filePath.startsWith(this.normalizedStoragePath)) {
            this.clearCachedData();
        } else if (this.filesChanged) {
            return; // fileMap or zip changed & we are rebuilding
        }
        this.filesChanged = true;
        this.LogInfo(`File changed: ${filePath.replace(this.rootPluginPath, "")}`);
        this.rebuildFiles();
    }

    private onFileUnlink(filePath: string) {
        this.filesChanged = true;
        this.LogInfo(`File deleted: ${filePath.replace(this.rootPluginPath, "")}`);
        this.rebuildFiles();
    }

    public rebuildFiles = this.debounce(() => {
        const _this = this;
        _this.rebuildFilesNow();
    }, 1000);

    private buildingInProgress = false;
    private buildsWaiting = 0;
    private async rebuildFilesNow() {
        if (!this.filesChanged) {
            return;
        }
        if (this.buildsWaiting > 0) {
            return;
        }
        this.buildsWaiting++;
        while (this.buildingInProgress) {
            await this.sleep(50);
        }
        this.buildsWaiting--;

        this.LogInfo("Rebuilding files", true);
        this.buildingInProgress = true;
        this.clearCachedData();
        const pluginFileMap = await this.createNewData();
        if (!pluginFileMap) {
            this.LogError("Failed to create new data");
            this.buildingInProgress = false;
            return;
        }
        this.cachedPluginFileMap = pluginFileMap;
        this.cachedPluginFileMapJson = JSON.stringify(pluginFileMap);
        setTimeout(() => {
            this.buildingInProgress = false;
            this.LogInfo("Rebuilding files done", true);
            this.filesChanged = false;
        }, 500);
    }

    private clearCachedData() {
        this.cachedPluginFileMap = null;
        this.cachedPluginFileMapJson = null;
    }

    private async createNewData(writeFiles = true): Promise<PluginFileMap> {
        const startTime = new Date().getTime();
        const zip = new jszip();
        const map: FileMap = new FileMap();
        const files = this.getFilesRecursively(this.bepinexFilePath);

        for (const file of files) {
            const data: Buffer = fs.readFileSync(`${file}`);
            const sha256Hash = this.hashUtil.generateHashForData("sha256", data);
            const relativePath = file.replace(this.bepinexFilePath, "");
            map.addFile(relativePath, sha256Hash, data.length);
            if (writeFiles) {
                zip.file(relativePath, data, { binary: true, createFolders: true, compression: "DEFLATE", compressionOptions: { level: 1 } }); // level 1 is fastest and in my testing the time/compression ratio doesn't get better with higher levels
            }
        }

        const pluginFileMap = new PluginFileMap();
        pluginFileMap.Files = map.getFiles();

        if (Object.keys(pluginFileMap.Files).length === 0) {
            pluginFileMap.FilesHash = "";
            pluginFileMap.Zip = null;

            try {
                this.vfs.writeFile(`${this.storagePath}fileMap.json`, JSON.stringify(pluginFileMap), false, true);
                this.vfs.removeFile(`${this.storagePath}bepinex.zip`);
            } catch (e) {
                if (!e.message.includes("ENOENT")) {
                    this.LogError(e.stack);
                }
            }

            return pluginFileMap;
        }

        // generate hashes
        pluginFileMap.FilesHash = this.hashUtil.generateHashForData("sha256", JSON.stringify(pluginFileMap.Files));
        if (!writeFiles) {
            return pluginFileMap;
        }

        const zipFileBuffer = await zip.generateAsync({ type: "nodebuffer" });
        pluginFileMap.Zip = {
            Name: "bepinex.zip",
            Hash: this.hashUtil.generateHashForData("sha256", zipFileBuffer),
            Size: zipFileBuffer.length
        } as File;

        fs.writeFileSync(`${this.storagePath}bepinex.zip`, zipFileBuffer);
        this.vfs.writeFile(`${this.storagePath}fileMap.json`, JSON.stringify(pluginFileMap), false, true);
        const endTime = new Date().getTime();
        this.LogInfo(`Rebuilt files in ${endTime - startTime}ms`, true);
        return pluginFileMap;
    }

    private async FilesDidChange(fileMap: PluginFileMap): Promise<boolean> {
        const currentFileMap = await this.createNewData(false);
        if (!currentFileMap || !fileMap) {
            if (!currentFileMap) {
                this.LogError("Current file map is null");
            }
            if (!fileMap) {
                this.LogError("File map is null");
            }
            return true;
        }
        if (currentFileMap.FilesHash !== fileMap.FilesHash) {
            const hash = currentFileMap.FilesHash === null || currentFileMap.FilesHash.length === 0 ? "empty" : currentFileMap.FilesHash;
            this.LogInfo(`Files hash changed: ${hash} != ${fileMap.FilesHash}`);
            return true;
        }
        return false;
    }

    private sleep(ms: number) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    private generateFileHash(file: string): string {
        const data = fs.readFileSync(`${file}`);
        return this.hashUtil.generateHashForData("sha256", data);
    }

    private getFilesRecursively(directory: string): string[] {
        let currentDirectory = directory;
        if (currentDirectory.endsWith("/")) {
            currentDirectory = currentDirectory.substring(0, currentDirectory.length - 1);
        }
        const files = this.vfs.getFiles(currentDirectory);
        for (let i = 0; i < files.length; i++) {
            files[i] = `${currentDirectory}/${files[i]}`;
        }
        const dirs = this.vfs.getDirs(currentDirectory);
        for (const dir of dirs) {
            files.push(...this.getFilesRecursively(`${currentDirectory}/${dir}`));
        }
        return files;
    }

    // Debounce for file changes
    // biome-ignore lint/suspicious/noExplicitAny: we can't know the type of the function
    private debounce<T extends (...args: any[]) => any>(func: T, wait: number): T {
        let timeout: NodeJS.Timeout | null;
        // biome-ignore lint/suspicious/noExplicitAny: we can't know the type of the function
        return ((...args: any[]) => {
            const later = () => {
                timeout = null;
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        }) as T;
    }

    private generateDefaultModConfig(): ModConfig {
        return {
            Debug: false,
            ClientOptions: {
                SyncType: Synchronization.DeleteAndSync
            }
        }
    }


    // Logging
    private LogInfo(message: string, alwaysLog = false) {
        if (this.modConfig.Debug || alwaysLog) {
            this.logger.info(`[${modName}] I: ${message}`);
        }
    }

    private LogWarning(message: string) {
        if (this.modConfig.Debug) {
            this.logger.warning(`[${modName}] W: ${message}`);
        }
    }

    private LogError(message: string) {
        this.logger.error(`[${modName}] E: ${message}`);
    }

    private LogDebug(message: string) {
        if (this.modConfig.Debug) {
            this.logger.debug(`[${modName}] D: ${message}`);
        }
    }
}

module.exports = { mod: new RemotePlugins() }
