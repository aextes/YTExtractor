﻿using System.Threading.Tasks;

using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System;
using System.IO;
using System.Linq;
using System.Web;

using Syroot.Windows.IO;

using NReco.VideoConverter;
using System.Linq.Expressions;
using YTExtractor;
using Google.Apis.YouTube.v3.Data;

namespace ConsoleApp1
{
    internal class YTAudioExtractor
    {
        private YouTubeService youtubeService;
        private YoutubeClient youtubeClient;
        private string downloadPath = KnownFolders.Downloads.Path;
        private string tmpPath = "tmp";

        public YTAudioExtractor()
        {
            youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyBUS_UGZMp2Hjr6k4Pbn3Azw93hkdOBKwE",
                ApplicationName = this.GetType().ToString()
            });
            youtubeClient = new YoutubeClient();
        }

        /// <summary>
        /// Проверяет, является ли введенная строка действующей ссылкой
        /// </summary>
        /// <param name="request">Строка для проверки</param>
        /// <returns>Возвращает результат проверки</returns>
        public bool IsUrl(string request)
        {
            if (Uri.IsWellFormedUriString(request, UriKind.Absolute))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Извлекает ID видео Youtube по переданной ссылке
        /// </summary>
        /// <param name="url"></param>
        /// <returns>Возвращает ID видео, если ссылка действительна и видео доступно</returns>
        public string ParseVideoId(string url)
        {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);

            string videoId;

            if (query.AllKeys.Contains("v"))
            {
                videoId = query["v"];
            }
            else
            {
                videoId = uri.Segments.Last();
            }

            return videoId;
        }

        /// <summary>
        /// Возвращает информацию о видео по переданной ссылке/id
        /// </summary>
        /// <param name="videoId"></param>
        /// <returns></returns>
        public VideoData GetVideoInfoAsync(string videoId)
        {
            if (IsUrl(videoId)) { videoId = ParseVideoId(videoId); }
            //return await youtubeClient.Videos.GetAsync(videoId);
            VideosResource.ListRequest listRequest = youtubeService.Videos.List("snippet");
            listRequest.Id = videoId;
            VideoListResponse response = listRequest.Execute();
            var r = response.Items.First().Snippet;
            VideoData vd = new VideoData();
            vd.id = videoId;
            vd.title = r.Title;
            vd.thumbnail = r.Thumbnails.High.Url;
            vd.channelId = r.ChannelId;
            vd.channelTitle = r.ChannelTitle;
            ChannelsResource.ListRequest request = youtubeService.Channels.List("snippet");
            request.Id = r.ChannelId;
            vd.channelThumbnail = request.Execute().Items.First().Snippet.Thumbnails.High.Url;
            return vd;
        }

        /// <summary>
        /// Возвращает ссылку на аудиопоток из видео в формате webm, если видео по ссылке доступно
        /// </summary>
        /// <param name="videoId"></param>
        /// <returns></returns>
        public async Task<string> GetAudioUrlAsync(string videoId)
        {
            if (IsUrl(videoId)) { videoId = ParseVideoId(videoId); }
            var audioStreams = await youtubeClient.Videos.Streams.GetManifestAsync(videoId).ConfigureAwait(false);
            var audioStreamInfo = audioStreams.GetAudioOnlyStreams().GetWithHighestBitrate();
            return audioStreamInfo.Url;
        }

        /// <summary>
        /// Возвращает экземляр потока аудио из видео по переданной ссылке/id
        /// </summary>
        /// <param name="videoId"></param>
        /// <returns></returns>
        public async Task<IStreamInfo> GetAudioStreamAsync(string videoId)
        {
            if (IsUrl(videoId)) { videoId = ParseVideoId(videoId); }
            var audioStreams = await youtubeClient.Videos.Streams.GetManifestAsync(videoId).ConfigureAwait(false);
            var audioStreamInfo = audioStreams.GetAudioOnlyStreams().GetWithHighestBitrate();
            return audioStreamInfo;
        }

        /// <summary>
        /// Скачивает аудио в формате mp3 из видео по переданной ссылке/id
        /// </summary>
        /// <param name="videoId"></param>
        /// <returns></returns>
        public async Task Extract(string videoId)
        {
            VideoData video = GetVideoInfoAsync(videoId);
            IStreamInfo streamInfo = await GetAudioStreamAsync(videoId);
            string webmpath = $"{downloadPath}\\{tmpPath}\\{video.title}.{streamInfo.Container}";
            string mp3path = $"{downloadPath}\\{video.title}.{Container.Mp3}";
            if (!System.IO.Directory.Exists(downloadPath + "\\" + tmpPath))
                try
                { System.IO.Directory.CreateDirectory(downloadPath + "\\" + tmpPath); }
                catch (Exception e)
                { System.Diagnostics.Debug.WriteLine(e.ToString()); }
            await youtubeClient.Videos.Streams.DownloadAsync(streamInfo, webmpath);
            WebmToMp3(webmpath, mp3path, true);
        }

        /// <summary>
        /// Конвертирует аудио файл формата webm в mp3
        /// </summary>
        /// <param name="inputPath">Путь оригинала</param>
        /// <param name="outputPath">Путь конвертированного файла</param>
        /// <param name="deleteOriginal">Нужно ли удалять оригинал</param>
        public void WebmToMp3(string inputPath, string outputPath, bool deleteOriginal = true)
        {
            var ffmpeg = new FFMpegConverter();
            ffmpeg.ConvertMedia(inputPath, outputPath, "mp3");

            if (deleteOriginal)
            {
                if (File.Exists(inputPath))
                {
                    File.Delete(inputPath);
                }
            }
        }

        /// <summary>
        /// Возвращает информацию о плейлисте по введенной ссылке
        /// </summary>
        /// <returns></returns>
        public async Task<PlaylistData> GetPlaylistData(string url)
        {
            var playlistData = new PlaylistData();
            var playlist = await youtubeClient.Playlists.GetAsync(url);
            playlistData.playlistInfo = playlist;
            //await foreach (var batch in youtubeClient.Playlists.GetVideoBatchesAsync(url))
            //{
            //    foreach (var video in batch.Items)
            //    {
            //        playlistData.vids.Append<PlaylistVideo>(video);
            //    }
            //}
            return playlistData;
        }

        /// <summary>
        /// Устаналивает выбранную директорию в качестве папки для сохранения файлов.
        /// </summary>
        /// <param name="path"></param>
        public void SetDownloadPath(string path)
        {
            downloadPath = path;
        }
    }
}