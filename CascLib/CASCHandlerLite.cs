﻿using System;
using System.Collections.Generic;
using System.IO;

namespace CASCExplorer
{
    public sealed class CASCHandlerLite : CASCHandlerBase
    {
        private Dictionary<ulong, MD5Hash> HashToKey = new Dictionary<ulong, MD5Hash>();
        private Dictionary<int, ulong> FileDataIdToHash = new Dictionary<int, ulong>();

        private CASCHandlerLite(CASCConfig config, LocaleFlags locale, BackgroundWorkerEx worker) : base(config, worker)
        {
            if (config.GameType != CASCGameType.WoW)
                throw new Exception("Unsupported game " + config.BuildUID);

            Logger.WriteLine("CASCHandlerLite: loading encoding data...");

            EncodingHandler EncodingHandler;

            using (var _ = new PerfCounter("new EncodingHandler()"))
            {
                using (var fs = OpenEncodingFile(this))
                    EncodingHandler = new EncodingHandler(fs, worker);
            }

            Logger.WriteLine("CASCHandlerLite: loaded {0} encoding data", EncodingHandler.Count);

            Logger.WriteLine("CASCHandlerLite: loading root data...");

            WowRootHandler RootHandler;

            using (var _ = new PerfCounter("new RootHandler()"))
            {
                using (var fs = OpenRootFile(EncodingHandler, this))
                    RootHandler = new WowRootHandler(fs, worker);
            }

            Logger.WriteLine("CASCHandlerLite: loaded {0} root data", RootHandler.Count);

            RootHandler.SetFlags(locale, ContentFlags.None, false);

            RootEntry rootEntry;

            foreach (var entry in RootHandler.GetAllEntries())
            {
                rootEntry = entry.Value;

                if ((rootEntry.LocaleFlags == locale || (rootEntry.LocaleFlags & locale) != LocaleFlags.None) && (rootEntry.ContentFlags & ContentFlags.LowViolence) == ContentFlags.None)
                {
                    EncodingEntry enc;

                    if (EncodingHandler.GetEntry(rootEntry.MD5, out enc))
                    {
                        if (!HashToKey.ContainsKey(entry.Key))
                        {
                            HashToKey.Add(entry.Key, enc.Key);
                            FileDataIdToHash.Add(RootHandler.GetFileDataIdByHash(entry.Key), entry.Key);
                        }
                    }
                }
            }

            RootHandler.Clear();
            RootHandler = null;
            EncodingHandler.Clear();
            EncodingHandler = null;
            GC.Collect();

            Logger.WriteLine("CASCHandlerLite: loaded {0} files", HashToKey.Count);
        }

        public static CASCHandlerLite OpenStorage(LocaleFlags locale, CASCConfig config, BackgroundWorkerEx worker = null)
        {
            return Open(locale, worker, config);
        }

        public static CASCHandlerLite OpenLocalStorage(string basePath, LocaleFlags locale, BackgroundWorkerEx worker = null)
        {
            CASCConfig config = CASCConfig.LoadLocalStorageConfig(basePath);

            return Open(locale, worker, config);
        }

        public static CASCHandlerLite OpenOnlineStorage(string product, LocaleFlags locale, string region = "us", BackgroundWorkerEx worker = null)
        {
            CASCConfig config = CASCConfig.LoadOnlineStorageConfig(product, region);

            return Open(locale, worker, config);
        }

        private static CASCHandlerLite Open(LocaleFlags locale, BackgroundWorkerEx worker, CASCConfig config)
        {
            using (var _ = new PerfCounter("new CASCHandlerLite()"))
            {
                return new CASCHandlerLite(config, locale, worker);
            }
        }

        public bool FileExists(int fileDataId) => FileDataIdToHash.ContainsKey(fileDataId);

        public bool FileExists(string file) => FileExists(Hasher.ComputeHash(file));

        public bool FileExists(ulong hash) => HashToKey.ContainsKey(hash);

        public override Stream OpenFile(int filedata)
        {
            ulong hash;

            if (FileDataIdToHash.TryGetValue(filedata, out hash))
                return OpenFile(hash);

            return null;
        }

        public override Stream OpenFile(string name) => OpenFile(Hasher.ComputeHash(name));

        public override Stream OpenFile(ulong hash)
        {
            MD5Hash key;

            if (HashToKey.TryGetValue(hash, out key))
                return OpenFile(key);

            return null;
        }
    }
}
