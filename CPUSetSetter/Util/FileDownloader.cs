using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CPUSetSetter.UI.Tabs.Processes;
using Velopack.Sources;

namespace CPUSetSetter.Util
{
    /// <summary>
    /// Default implementation of file downloader using HttpClient
    /// </summary>
    public class FileDownloader : Velopack.Sources.IFileDownloader
    {
        private static readonly HttpClient _httpClient = new();

        public async Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeout));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    WindowLogger.Write($"Download failed with status code: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                }

                long? totalBytes = response.Content.Headers.ContentLength ?? 0;
                long downloadedBytes = 0;

                using Stream contentStream = await response.Content.ReadAsStreamAsync();
                using FileStream fileStream = new(targetFile, FileMode.Create, FileAccess.Write, FileShare.None);

                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    
                    int progressPercent = totalBytes > 0 ? (int)((downloadedBytes * 100) / totalBytes) : 0;
                    progress?.Invoke(progressPercent);
                }
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"File download failed: {ex.Message}");
                throw;
            }
        }

        public async Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeout));

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(cts.Token);
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Download bytes failed: {ex.Message}");
                throw;
            }
        }

        public async Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeout));

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cts.Token);
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Download string failed: {ex.Message}");
                throw;
            }
        }
    }
}