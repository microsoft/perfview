namespace PerfDataService
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Diagnostics.Tracing.StackSources;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Extensions.Caching.Memory;
    using System.Text;

    public sealed class CallTreeDataProviderFactory : ICallTreeDataProviderFactory
    {
        private const string etlExtension = ".etl";

        private const string etlxExtension = ".etlx";

        private readonly HttpRequest httpRequest;

        private readonly TextWriter textWriter;

        private readonly EtlxCache etlxCache;

        private readonly StackViewerSessionCache stackViewerSessionCache;

        private readonly DateTimeOffset cacheExpirationTime;

        private readonly string tempPath;

        public CallTreeDataProviderFactory(IHttpContextAccessor httpContextAccessor, ITemporaryPathProvider temporaryPathProvider, ICacheExpirationTimeProvider cacheExpirationTimeProvider, TextWriter textWriter, EtlxCache etlxCache, StackViewerSessionCache stackViewerSessionCache)
        {
            if (httpContextAccessor == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(httpContextAccessor));
            }

            if (temporaryPathProvider == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(temporaryPathProvider));
            }

            if (cacheExpirationTimeProvider == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(cacheExpirationTimeProvider));
            }

            if (textWriter == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(textWriter));
            }

            if (etlxCache == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(etlxCache));
            }

            if (stackViewerSessionCache == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stackViewerSessionCache));
            }

            this.httpRequest = httpContextAccessor.HttpContext.Request;
            this.textWriter = textWriter;
            this.tempPath = temporaryPathProvider.Path;
            this.cacheExpirationTime = cacheExpirationTimeProvider.Expiration;
            this.etlxCache = etlxCache;
            this.stackViewerSessionCache = stackViewerSessionCache;
        }

        public ICallTreeDataProvider Get()
        {
            var queryString = this.httpRequest.Query;

            string filename = queryString["filename"];
            string stacktype = queryString["stacktype"];

            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException("filename");
            }

            if (string.IsNullOrEmpty(stacktype))
            {
                throw new ArgumentNullException("stacktype");
            }

            /* symbols and sources related parameters */
            string sympath = (string)queryString["sympath"] ?? string.Empty;
            string srcpath = (string)queryString["srcpath"] ?? string.Empty;
            string imageFilter = (string)queryString["imagefilter"] ?? string.Empty;
            string modulesFilter = (string)queryString["modulesfilter"] ?? string.Empty;

            /* filtering parameters */
            string start = (string)queryString["start"] ?? string.Empty;
            string end = (string)queryString["end"] ?? string.Empty;
            string incpats = (string)queryString["incpats"] ?? string.Empty;
            string excpats = (string)queryString["excpats"] ?? string.Empty;
            string foldpats = (string)queryString["foldpats"] ?? string.Empty;
            string grouppats = (string)queryString["grouppats"] ?? string.Empty;
            string foldpct = (string)queryString["foldpct"] ?? string.Empty;

            EtlxFile etlxFile;

            string etlxFilePath = Path.Combine(tempPath, Path.ChangeExtension(filename.Replace(@"\", "_").Replace(@":", "_").Replace(@"/", "_"), etlxExtension));
            SymbolReader symbolReader;

            lock (this.etlxCache)
            {
                if (this.etlxCache.TryGetValue(filename, out etlxFile))
                {
                    if (etlxFile == null)
                    {
                        throw new ArgumentNullException("etlxFile");
                    }
                }
                else
                {
                    etlxFile = new EtlxFile(filename) { Pending = true };
                    this.etlxCache.Set(filename, etlxFile, this.cacheExpirationTime);
                }
            }

            lock (etlxFile)
            {
                if (etlxFile.Pending)
                {
                    if (!File.Exists(etlxFilePath))
                    {
                        // if it's a zip file
                        if (string.Equals(Path.GetExtension(filename), ".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            using (ZipArchive archive = ZipFile.OpenRead(filename))
                            {
                                foreach (ZipArchiveEntry entry in archive.Entries)
                                {
                                    if (entry.FullName.EndsWith(etlExtension, StringComparison.OrdinalIgnoreCase))
                                    {
                                        entry.ExtractToFile(Path.ChangeExtension(etlxFilePath, etlExtension));
                                        break;
                                    }
                                }
                            }

                            TraceLog.CreateFromEventTraceLogFile(Path.ChangeExtension(etlxFilePath, etlExtension), etlxFilePath);
                        }
                        else
                        {
                            TraceLog.CreateFromEventTraceLogFile(filename, etlxFilePath);
                        }
                    }

                    etlxFile.TraceLog = TraceLog.OpenOrConvert(etlxFilePath);
                    etlxFile.Pending = false;
                    var processes = etlxFile.TraceLog.Processes;

                    symbolReader = new SymbolReader(this.textWriter, sympath) { SourcePath = srcpath };

                    foreach (var process in processes)
                    {
                        if (string.IsNullOrEmpty(imageFilter) || string.Equals(process.ImageFileName, imageFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var module in process.LoadedModules)
                            {
                                if (string.IsNullOrEmpty(modulesFilter) || modulesFilter.Split(',').Contains(module.Name))
                                {
                                    //
                                    // TODO: lt72: why is this exception occurring and do we care? (it seems like we are just trying to load symbols...)
                                    //
                                    try
                                    {
                                        etlxFile.TraceLog.CodeAddresses.LookupSymbolsForModule( symbolReader, module.ModuleFile );
                                    }
                                    catch
                                    {

                                    }
                                }
                            }
                        }
                    }
                }
            }

            StackViewerSession stackViewerSession;
            lock (this.stackViewerSessionCache)
            {
                var filterParams = new FilterParams { Name = filename + stacktype, StartTimeRelativeMSec = start, EndTimeRelativeMSec = end, MinInclusiveTimePercent = foldpct, FoldRegExs = foldpats, IncludeRegExs = incpats, ExcludeRegExs = excpats, GroupRegExs = grouppats };
                var keyBuilder = new StringBuilder();
                keyBuilder.Append(filterParams.Name).Append("?" + filterParams.StartTimeRelativeMSec).Append("?" + filterParams.EndTimeRelativeMSec).Append("?" + filterParams.MinInclusiveTimePercent).Append("?" + filterParams.FoldRegExs).Append("?" + filterParams.IncludeRegExs).Append("?" + filterParams.ExcludeRegExs).Append("?" + filterParams.GroupRegExs); 
                
                var stackViewerKey = keyBuilder.ToString();
                if (this.stackViewerSessionCache.TryGetValue(stackViewerKey, out stackViewerSession))
                {
                    if (stackViewerSession == null)
                    {
                        throw new ArgumentNullException("stackViewerSession");
                    }
                }
                else
                {
                    var processes = etlxFile.TraceLog.Processes;
                    symbolReader = new SymbolReader(this.textWriter, sympath) { SourcePath = srcpath };

                    foreach (var process in processes)
                    {
                        if (string.IsNullOrEmpty(imageFilter) || string.Equals(process.ImageFileName, imageFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var module in process.LoadedModules)
                            {
                                if (string.IsNullOrEmpty(modulesFilter) || modulesFilter.Split(',').Contains(module.Name))
                                {
                                    //
                                    // TODO: lt72: why is this exception occurring and do we care? (it seems like we are just trying to load symbols...)
                                    //
                                    try
                                    {
                                        etlxFile.TraceLog.CodeAddresses.LookupSymbolsForModule( symbolReader, module.ModuleFile );
                                    }
                                    catch
                                    {

                                    }
                                }
                            }
                        }
                    }

                    stackViewerSession = new StackViewerSession(filename, stacktype, etlxFile.TraceLog, filterParams, symbolReader);
                    this.stackViewerSessionCache.Set(stackViewerKey, stackViewerSession, cacheExpirationTime);
                }
            }

            lock (stackViewerSession)
            {
                if (stackViewerSession.Pending)
                {
                    stackViewerSession.InitializeDataProvider();
                    stackViewerSession.Pending = false;
                }
            }

            return stackViewerSession.GetDataProvider();
        }
    }
}