using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NSENifty50Feeder.Data.Filemodel;

namespace NSENifty50Feeder.Helper
{ 
    public class FileService
    {

        private readonly HttpClient _httpClient;
        private HubConnection? _hubConnection;
        private readonly ILogger<FileService> _logger;
        private string? connectionId = string.Empty;
        public delegate void NewSpan(string path);
        public event NewSpan? OnNewSpanFile;
        private bool WaitFile = false;

        public FileService(ILogger<FileService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;

        }


        public async Task<DownloadResult> DownloadAsync(FileType fileType)
        {
            try
            {
                var isDownloadRequired = await IsDownloadRequired(fileType);
                if (isDownloadRequired != null)
                {
                    if (isDownloadRequired.Item1)
                    {
                        var response = await _httpClient.GetAsync($"/file/{fileType}");
                        if (response.IsSuccessStatusCode)
                        {

                            string filePath = GetFilePath(response);

                            var stream = await response.Content.ReadAsStreamAsync();

                            if (await SaveFileFromStreamAsync(stream, filePath))
                            {
                                Unzip(filePath, isDownloadRequired.Item2);
                            }

                        }
                        else
                        {
                            _logger.LogWarning($"Unable download {isDownloadRequired.Item2}");
                        }

                    }

                    return new DownloadResult { FileName = isDownloadRequired.Item2, Type = fileType };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError($"Request timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
            }
            return null;
        }
        private async Task<Tuple<bool, string>?> IsDownloadRequired(FileType fileType)
        {
            try
            {

                Datum? datum = null;

                var response = await _httpClient.GetAsync($"/latest/{fileType}");
                if (response.IsSuccessStatusCode)
                {
                    string fileData = await response.Content.ReadAsStringAsync();
                    datum = Newtonsoft.Json.JsonConvert.DeserializeObject<Datum>(fileData);
                    if (datum != null && !string.IsNullOrEmpty(datum.filename))
                    {
                        string filePath = GetFilePath(null, datum.filename);
                        var fileInfo = new FileInfo(filePath);
                        if (!File.Exists(filePath) || fileInfo.LastWriteTime.Date < datum.lastUpdated.Date)
                        {
                            return Tuple.Create(true, filePath);
                        }
                        else
                        {
                            return Tuple.Create(false, filePath);
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError($"Request timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
            }

            return null;

        }

        public string GetFilePath(HttpResponseMessage? response, string filePath = "")
        {
            var fileName = string.IsNullOrEmpty(filePath) ? response?.Content.Headers.ContentDisposition?.FileName ?? "downloadedFile" : filePath;
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FTP");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, fileName);
        }
        private async Task<bool> SaveFileFromStreamAsync(Stream contentStream, string destinationPath)
        {
            try
            {
                if (contentStream == null || string.IsNullOrEmpty(destinationPath)) return false;
                const int bufferSize = 81920; // 80 KB buffer size
                byte[] buffer = new byte[bufferSize];
                int bytesRead;

                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true))
                {
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace, ex.Message);
            }
            return false;

        }
        private void Unzip(string zipFileName, string unzipPath)
        {
            try
            {
                string ext = Path.GetExtension(zipFileName);
                if (ext == ".gz" || ext == ".zip")
                {

                    var folderName = Path.GetFileNameWithoutExtension(zipFileName);

                    if (ext == ".zip")
                    {
                        ZipFile.ExtractToDirectory(zipFileName, unzipPath, true);
                        File.Delete(zipFileName);

                    }
                    else
                    {
                        Decompress(new FileInfo(zipFileName), unzipPath);
                        File.Delete(zipFileName);

                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace, ex.Message);
            }

        }
        public void Decompress(FileInfo fileToDecompress, string newFileName)
        {
            try
            {
                using (FileStream originalFileStream = fileToDecompress.OpenRead())
                {
                    string currentFileName = fileToDecompress.FullName;


                    using (FileStream decompressedFileStream = File.Create(newFileName))
                    {
                        using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                        {
                            decompressionStream.CopyTo(decompressedFileStream);
                            Console.WriteLine("Decompressed: {0}", fileToDecompress.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace, ex.Message);
            }
        }
    }
    public class DownloadResult
    {
        public FileType Type { get; set; }
        public string? FileName { get; set; }
    }
    public enum FileType
    {
        contract = 1,
        security = 2,
        BhavCopy_NSE_FO = 3,
        SpanFile = 4,
        MD = 5,
        fo_secban = 6,
        C_VAR1 = 7,
        F_CN01_NSE = 8

    }
}
