using ATL;
using Genius;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Xml;
using TagLib;
using TagLib.Id3v2;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für SongList_Status.xaml
    /// </summary>
    public partial class SongList_Status : Window
    {
        public List<Song> songList = new();
        List<Song> FailedDownloads = new();
        List<Song> FinalFailedDownloads = new();
        List<Song> NonConvertableFiles = new();
        List<Song> SuccessfulDownloads = new();
        Process cmd;
        XmlWriter xmlWriter;
        BackgroundWorker worker;
        bool _shown;
        bool downloadStarted;
        bool retryDownload;
        bool addingLyrics;
        public bool allSongsDownloaded;
        string strCmdText;
        string downloadType;
        string extension;
        string subject, textBody;

        public SongList_Status(List<Song> songList, string downloadType)
        {
            InitializeComponent();
            this.songList = songList;
            this.downloadType = downloadType;
            lblStatus.Content = "Download started...";
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (_shown)
                return;
            _shown = true;

            switch (downloadType)
            {
                case ".mp3":
                    strCmdText = "yt-dlp -x --audio-format mp3 ";
                    break;

                case ".wav":
                    strCmdText = "yt-dlp -x --audio-format wav ";
                    break;

                case ".opus":
                    strCmdText = "yt-dlp -x --audio-quality 0 ";
                    break;

                case ".m4a":
                    strCmdText = "yt-dlp -f ba[ext=m4a] ";
                    break;

                case ".mp4":
                    strCmdText = "yt-dlp -S res,ext:mp4:m4a --recode mp4 ";
                    break;

                case "best audio format":
                    strCmdText = "yt-dlp -f bestaudio ";
                    break;

                case "best video format":
                    strCmdText = "yt-dlp -f bestvideo+bestaudio ";
                    break;
            }


            cmd = new Process();

            // access command prompt
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = @"youtube-dl\";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;

            // backgroundworker is used as to enable reporting progress to the label
            BackgroundWorker worker = new();
            worker.WorkerReportsProgress = true;
            worker.DoWork += new DoWorkEventHandler(Worker_DoWork);
            worker.ProgressChanged += new ProgressChangedEventHandler(Worker_ProgressChanged);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Worker_RunWorkerCompleted);
            worker.WorkerSupportsCancellation = true;

            if (!worker.IsBusy)
                worker.RunWorkerAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // create the file to write in
            string filename = @"downloaded songs\downloads " + DateTime.Now.ToString("yMMdd-HHmmss") + ".xml";
            FileStream filestream = System.IO.File.Create(filename);
            filestream.Close();
            xmlWriter = XmlWriter.Create(filename);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteWhitespace("\n");
            xmlWriter.WriteStartElement("Songs");
            xmlWriter.WriteWhitespace("\n");

            worker = (BackgroundWorker)sender;

            worker.ReportProgress(0, "Accessing Spotify API...\n" + @"Adding ""Genres"" and ""Album"" tags...");
            Thread.Sleep(500); // display the message a bit longer for it to be readable

            // call Spotify API -> add genres and album
            string token = Spotify.GetAccessToken();
            if (token == "ConnectionFailed")
                return;
            SpotifyClient spotifyClient = new(token);

            int songIndex = 0;
            foreach (var song in songList)
            {
                if (worker.CancellationPending == true) // window has been closed -> cancel backgroundworker
                {
                    e.Cancel = true;
                    return;
                }

                if (song.Genres == null && song.Album == null) // information not yet added to song
                {
                    try
                    {
                        SearchRequest searchRequest = new(SearchRequest.Types.Artist, song.Artist);
                        var searchResponse = spotifyClient.Search.Item(searchRequest);
                        var artist = searchResponse.Result.Artists.Items[0];
                        var genres = spotifyClient.Artists.Get(artist.Id).Result.Genres.ToArray();
                        song.Genres = AddGenres(genres);

                        searchRequest = new(SearchRequest.Types.Track, song.Artist + " " + song.Songname);
                        searchResponse = spotifyClient.Search.Item(searchRequest);
                        var track = searchResponse.Result.Tracks.Items[0];
                        song.Album = spotifyClient.Tracks.Get(track.Id).Result.Album.Name;
                    }
                    catch (Exception exception)
                    {
                        // ArgumentOutOfRangeException = thrown when handling with yt playlists -> no clear artist/songname
                        // AggregateException = Spotify servers unavailable (should be resolved within a few hours on server side)
                        if (!(exception is ArgumentOutOfRangeException || exception is AggregateException))
                            throw;
                        // when these exceptions are thrown -> continue normally with rest of code
                    }
                }

                songIndex++;
                worker.ReportProgress(songIndex, song.Artist + " - " + song.Songname);
            }

            worker.ReportProgress(0, "Adding lyrics...");

            songIndex = 0;
            addingLyrics = true;
            foreach (var song in songList)
            {
                // for some dubious reason the lyrics can't be found in the Genius API directly.
                // Therefore I've decided to first access the API to get the regular path to the article
                // and then to simply web scrape the lyrics
                // well-written article suggesting this approach: https://bigishdata.com/2016/09/27/getting-song-lyrics-from-geniuss-api-scraping/

                string link = "https://api.genius.com/search?q=";
                string searchtext = Regex.Replace(song.Artist + " " + song.Songname, " ", "%20");
                link += searchtext;

                string webData = GetLyricsWebData(link, true);

                int startIndex = webData.IndexOf(@"""url"":") + 7;
                int stopIndex = webData.IndexOf(',', startIndex) - 1;
                link = webData[startIndex..stopIndex];

                webData = GetLyricsWebData(link, false);

                if (webData != null && !webData.Contains("Lyrics for this song have yet to be released.") && !webData.Contains("The lyrics for this song have yet to be transcribed.")) // website/lyrics couldn't be accessed
                {
                    // web scraping (could be prone to errors if genius.com decides to redesign their html)
                    string lyrics = "";
                    startIndex = webData.IndexOf("Lyrics__Container-sc-1ynbvzw-5 Dzxov"); // defining the bounds in which to look for the lyrics -> hard coded indices
                    stopIndex = webData.IndexOf("div class=\"ShareButtons__Root", startIndex);
                    string lyricsWebData = webData[startIndex..stopIndex];
                    int stopIndex1, stopIndex2;
                    stopIndex = 0;

                    // remove italics tags
                    lyricsWebData = lyricsWebData.Replace("<i>", "");
                    lyricsWebData = lyricsWebData.Replace("</i>", "");

                    do
                    {
                        // lyrics can be found within <br> (plain lyrics) and <span> (annotated lyrics) tags
                        stopIndex1 = lyricsWebData.IndexOf("<br/>", stopIndex + 1);
                        stopIndex2 = lyricsWebData.IndexOf("</span>", stopIndex + 1);

                        List<int> closestStopIndex = new List<int>() { stopIndex1, stopIndex2 };

                        for (int i = 1; i >= 0; i--)
                        {
                            if (closestStopIndex[i] == -1)
                                closestStopIndex.RemoveAt(i);
                        }

                        if (closestStopIndex.Count != 0)
                            stopIndex = closestStopIndex.Min();
                        else
                            break;

                        if (stopIndex > lyricsWebData.Length)
                            break;

                        startIndex = lyricsWebData.LastIndexOf('>', stopIndex) + 1; // defining the bounds of the tag value

                        if (startIndex < lyricsWebData.Length && startIndex != stopIndex) // still within search bounds && not an empty tag
                            lyrics += lyricsWebData[startIndex..stopIndex] + "\n"; // reading in the tag value
                    }
                    while (startIndex < lyricsWebData.Length);

                    // add last lyric line, as it sometimes isn't enclosed by a <br> tag
                    int tempIndex = startIndex;
                    startIndex = stopIndex; // missing tag is right after last read tag

                    // decide wether the last tag was </br> or </span>
                    if (lyricsWebData.IndexOf("<br/>", tempIndex) != -1)
                        startIndex += 5; // "<br/>
                    else
                        startIndex += 7; // </span>"

                    stopIndex = lyricsWebData.IndexOf('<', startIndex);
                    lyrics += lyricsWebData[startIndex..stopIndex];

                    song.Lyrics = lyrics;

                    songIndex++;
                    worker.ReportProgress(songIndex, song.Artist + " - " + song.Songname);
                }
            }

            addingLyrics = false;
            downloadStarted = true;
            worker.ReportProgress(0, "Download started...");

            songIndex = 0;
            foreach (var song in songList)
            {
                songIndex++;

                if (song.Link == null)
                    song.Link = "https://www.youtube.com/watch?v=" + song.ID;

                // delete excess files
                var filePaths = Directory.GetFiles("youtube-dl");

                foreach (var filePath in filePaths)
                {
                    try
                    {
                        if (!filePath.Contains(".exe"))
                            System.IO.File.Delete(filePath);
                    }
                    catch (IOException) // file is still used by another process -> delete later
                    { }
                }

                cmd.Start();
                cmd.StandardInput.WriteLine(strCmdText + song.Link);
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();

                ReportCmdProgress(song, songIndex);

                try
                { cmd.WaitForExit(); }
                catch (InvalidOperationException) { } // window has been closed while the process was running -> worker has already been closed

                ChangeProperties(song);

                if (worker.CancellationPending == true) // window has been closed -> cancel backgroundworker
                {
                    e.Cancel = true;
                    return;
                }

                WriteToXmlFile(song);

                if (!FailedDownloads.Contains(song) && !NonConvertableFiles.Contains(song))
                    SuccessfulDownloads.Add(song);
            }

            worker.ReportProgress(0, "All songs downloaded.");

            xmlWriter.WriteEndDocument();
            xmlWriter.Close();

            if (FailedDownloads.Count != 0)
            {
                retryDownload = true;

                // delete excess files created during aborted processes
                var filePaths = Directory.GetFiles("youtube-dl");
                foreach (var filePath in filePaths)
                {
                    try
                    {
                        if (!filePath.Contains(".exe"))
                            System.IO.File.Delete(filePath);
                    }
                    catch (IOException) // exception thrown if file is still used by another thread
                    { }
                }

                worker.ReportProgress(0, "Some songs were skipped.\nRetrying the download...");
                Thread.Sleep(500);

                // retry skipped songs
                songIndex = 1;
                foreach (var song in FailedDownloads)
                {
                    // update yt-dlp
                    cmd.Start();
                    cmd.StandardInput.WriteLine("yt-dlp -U");
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();

                    // retry download
                    cmd.Start();
                    cmd.StandardInput.WriteLine(strCmdText + song.Link);
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();

                    ReportCmdProgress(song, songIndex);

                    Task delay = Task.Delay(30000); // 30000ms = 30s
                    Task.WhenAny(cmd.WaitForExitAsync(), delay).Wait(); // if the song isn't downloaded after 30s, it is skipped

                    worker.ReportProgress(songIndex, song.Artist + " - " + song.Songname);

                    ChangeProperties(song);

                    if (worker.CancellationPending == true) // window has been closed -> cancel backgroundworker
                    {
                        e.Cancel = true;
                        return;
                    }

                    songIndex++;
                }

                worker.ReportProgress(0, "All songs downloaded.");

                // if downloads still fail
                if (FinalFailedDownloads.Count != 0)
                {
                    string failedsongsmessage = "";
                    foreach (var song in FinalFailedDownloads)
                    {
                        failedsongsmessage += song.Artist + " - " + song.Songname + "\n";
                    }

                    MessageBox.Show("Unfortunately the following songs couldn't be downloaded:\n\n" + failedsongsmessage +
                                  "\nIf the video is age restricted by YouTube, it won't be possible to download it." +
                                  "\nPlease try again." +
                                  "\n\nA report has been sent to the developer.",
                                  "Failed Downloads", MessageBoxButton.OK, MessageBoxImage.Error);

                    // send report to developer
                    subject = "YouTube-dl GUI => Failed Download";
                    textBody = "<pre>" +
                                      "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString() +
                                    "\nUser: " + Environment.UserName +
                                  "\n\nThe download of the following songs has failed:" +
                                      "<pre>";

                    foreach (var song in FinalFailedDownloads)
                    {
                        failedsongsmessage += "\n\n" + song.Artist + " - " + song.Songname +
                                                "\n" + song.Link +
                                                "\n" + song.Album +
                                                "\n" + song.Genres;
                    }

                    textBody += failedsongsmessage;

                    App.SendEmail(subject, textBody);
                }
            }

            if (NonConvertableFiles.Count != 0)
            {
                // send report and display message -> files couldn't be converted
                string reportMessage = "";
                foreach (var element in NonConvertableFiles)
                {
                    reportMessage += "\n\nArtist: " + element.Artist +
                                       "\nSongname: " + element.Songname +
                                       "\nLink: " + element.Link +
                                       "\nAlbum: " + element.Album +
                                       "\nGenres: " + element.Genres;
                }

                subject = "YouTube-dl GUI => FFMPEG conversion failed";
                textBody = "<pre>" +
                           "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString() +
                         "\nUser: " + Environment.UserName +
                       "\n\nThe following songs couln't be converted by ffmpeg:\n\n" +
                            reportMessage +
                           "<pre>";

                App.SendEmail(subject, textBody);
            }

            subject = "YouTube-dl GUI => Download completed";
            textBody = "<pre>" +
                       "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString() +
                     "\nUser: " + Environment.UserName +
                   "\n\nThis email notifies of the completion of a download." +
                   "\n\nDownloaded songs: " + songList.Count +
                     "\nRetried downloads: " + FailedDownloads.Count +
                     "\nFailed downloads: " + FinalFailedDownloads.Count +
                     "\nFailed conversions: " + NonConvertableFiles.Count +
                     "\nSuccessful downloads: " + SuccessfulDownloads.Count +
                       "<pre>";

            App.SendEmail(subject, textBody);

            allSongsDownloaded = true;
        }

        private void ReportCmdProgress(Song song, int songIndex)
        {
            string strPreviousOutput = "";
            bool outputStarted = false;
            bool exit = false;
            try
            {
                do
                {
                    string cmdOutput = cmd.StandardOutput.ReadLine();

                    if (cmdOutput.Contains("ETA") && cmdOutput != strPreviousOutput)
                    {
                        worker.ReportProgress(0, song.Artist + " - " + song.Songname + "\n" + cmdOutput); // display download progress
                        outputStarted = true;
                    }

                    strPreviousOutput = cmdOutput;

                    if (outputStarted && !cmdOutput.Contains("ETA"))
                    {
                        exit = true;
                        worker.ReportProgress(songIndex, song.Artist + " - " + song.Songname);
                    }
                }
                while (!cmd.HasExited && !exit);
            }
            catch (InvalidOperationException)
            { } // worker already closed
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblStatus.Content = (String)e.UserState;

            if (e.ProgressPercentage != 0)
            {
                if (retryDownload)
                    lblStatus.Content += "\ndownloaded " + e.ProgressPercentage.ToString() + "/" + FailedDownloads.Count;
                else
                {
                    if (addingLyrics)
                        lblStatus.Content += "\nLyrics added " + e.ProgressPercentage.ToString() + "/" + songList.Count;
                    else if (!downloadStarted)
                        lblStatus.Content += "\ntags added " + e.ProgressPercentage.ToString() + "/" + songList.Count;
                    else
                        lblStatus.Content += "\ndownloaded " + e.ProgressPercentage.ToString() + "/" + songList.Count;
                }
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Thread.Sleep(1000);
            Close();
        }

        private string GetLyricsWebData(string link, bool accessingAPI)
        {
            HttpClient client = new();
            var request = new HttpRequestMessage(HttpMethod.Get, link);
            if (accessingAPI)
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + Youtube.DecryptText(ConfigurationManager.AppSettings["GeniusAccessToken"]));
            HttpResponseMessage response;
            try { response = client.Send(request); }
            catch (InvalidOperationException)
            { return null; };
            var reader = new StreamReader(response.Content.ReadAsStream());
            string webData = reader.ReadToEnd();
            webData = HttpUtility.HtmlDecode(webData);

            return webData;
        }

        public static string AddGenres(string[] genres)
        {
            // .mp3 only stores first genre -> genres must be stored with delimiters (";") in first genre
            // (Windows Explorer might not show all genres, but they are stored)

            string combinedGenres = "";
            foreach (var genre in genres)
            {
                combinedGenres += genre + ";";
            }
            return combinedGenres;
        }

        private void WriteToXmlFile(Song song)
        {
            xmlWriter.WriteStartElement("Song");
            xmlWriter.WriteWhitespace("\n");

            xmlWriter.WriteStartElement("Name");
            xmlWriter.WriteString(song.Songname);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteWhitespace("\n");

            xmlWriter.WriteStartElement("Artist");
            xmlWriter.WriteString(song.Artist);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteWhitespace("\n");

            xmlWriter.WriteStartElement("Link");
            xmlWriter.WriteString(song.Link);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteWhitespace("\n");

            xmlWriter.WriteStartElement("Album");
            xmlWriter.WriteString(song.Album);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteWhitespace("\n");

            xmlWriter.WriteStartElement("Genres");
            xmlWriter.WriteString(song.Genres);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteWhitespace("\n");

            xmlWriter.WriteEndElement();
            xmlWriter.WriteWhitespace("\n\n");
        }

        private void ChangeProperties(Song song)
        {
            var directory = new DirectoryInfo(@"youtube-dl");
            string[] extensions = { "*.mp3", "*.wav", "*.opus", "*.m4a", "*.mp4", "*.webm" };

            FileInfo myFile;
            try
            {
                // get files
                var files = Directory.GetFiles(@"youtube-dl", "*.*", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".mp3") || x.EndsWith(".wav") || x.EndsWith(".opus") || x.EndsWith(".m4a") || x.EndsWith(".mp4") || x.EndsWith(".webm"));

                // convert string path to file info
                List<FileInfo> fileInfos = new();
                foreach (var filepath in files)
                {
                    fileInfos.Add(new FileInfo(filepath));
                }

                // find newest file
                myFile = fileInfos.OrderBy(f => f.LastWriteTime).First();

                extension = myFile.Extension;
            }
            catch (InvalidOperationException) // no files found
            {
                if (!retryDownload)
                {
                    FailedDownloads.Add(song);
                    worker.ReportProgress(0, song.Artist + " - " + song.Songname +
                                           "\ndownload failed - song was skipped");
                }
                else
                    FinalFailedDownloads.Add(song);

                return;
            }

            song.Artist = RemoveForbiddenCharacters(song.Artist);
            song.Songname = RemoveForbiddenCharacters(song.Songname);

            // edit metadata of file using ATL.NET
            // https://github.com/Zeugma440/atldotnet

            Track track = new(myFile.FullName);
            track.Artist = song.Artist;
            track.Title = song.Songname;
            track.Album = song.Album;
            track.Genre = song.Genres;
            track.Comment = song.Link;
            track.Lyrics.UnsynchronizedLyrics = song.Lyrics;
            track.Save();

            // set language of comment property as to make it visible in windows explorer
            if (extension == ".mp3" || extension == ".wav") // other extension types are not supported in TagLib
            {
                var file = TagLib.File.Create(myFile.FullName);
                TagLib.Id3v2.Tag tag = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
                CultureInfo cultureInfo = CultureInfo.InstalledUICulture;
                string language = cultureInfo.ThreeLetterWindowsLanguageName;
                CommentsFrame frame = CommentsFrame.Get(tag, song.Link, language, true);
                frame.Text = song.Link;
                file.Save();
            }

            MoveFile(myFile, song, "music/youtube-dl", 0);
        }

        public string RemoveForbiddenCharacters(string source)
        {
            // remove unwanted characters to be able to save the file
            char[] forbiddenCharacters = { '"', '＂', '*', '<', '>', '?', '？', '\\', '|', '｜', '/', '⧸', ':', '：' };

            foreach (char character in forbiddenCharacters)
            {
                if (source.Contains(character))
                {
                    if (source.Count(t => t == character) > 1) // more than one instance of the character
                        source = RemoveMultipleForbiddenCharacters(source, character);
                    else
                        source = source.Remove(source.IndexOf(character), 1);
                }
            }

            return source;
        }

        private string RemoveMultipleForbiddenCharacters(string source, char character)
        {
            source = source.Remove(source.LastIndexOf(character), 1);

            if (source.Contains(character))
                source = RemoveMultipleForbiddenCharacters(source, character);

            return source;
        }

        public void MoveFile(FileInfo file, Song song, string destination, int accountsOfFile)
        {
            string songInfo = song.Artist + " - " + song.Songname;

            // move file and change file name            
            try
            {
                if (accountsOfFile == 0)
                {
                    if (destination == "music/youtube-dl")
                        file.MoveTo(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + @"\youtube-dl\" + songInfo + extension);
                    else // called from Downloaded_ChangeSongInfo.xaml.cs
                    {
                        // overwrite file
                        System.IO.File.Delete(destination);
                        file.MoveTo(destination);
                    }
                }
                else
                {
                    if (destination == "music/youtube-dl")
                        file.MoveTo(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + @"\youtube-dl\" + songInfo + " (" + accountsOfFile + ')' + extension);
                    else
                    {
                        System.IO.File.Delete(destination);
                        file.MoveTo(destination);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is DirectoryNotFoundException)
                {
                    Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + @"\youtube-dl");
                    MoveFile(file, song, "music/youtube-dl", accountsOfFile);
                }
                else if (ex is IOException && !(ex is FileNotFoundException)) // file already exists
                {
                    accountsOfFile++;
                    MoveFile(file, song, destination, accountsOfFile);
                }
                else if (ex is FileNotFoundException)
                {
                    // file eg: ...(2).mp3 not found -> ffmpeg hasn't worked, as only file without (2) exists
                    NonConvertableFiles.Add(song);
                }
            }
        }

        private async void AddLyrics(Song song)
        {
            // access Genius API
            string apiKey = Youtube.DecryptText(ConfigurationManager.AppSettings["GeniusClientId"]);
            var geniusClient = new GeniusClient(apiKey);

            // search for song
            var search = await geniusClient.SearchClient.Search(song.Artist + " " + song.Songname);
            var songId = search.Response;

            // get lyrics
            var lyrics = await geniusClient.SongClient.GetSong(378195);

            // add lyrics to song


        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (!allSongsDownloaded)
            {
                try
                {
                    xmlWriter.WriteEndDocument();
                    xmlWriter.Close();
                }
                catch (InvalidOperationException) // writer is already closed
                { }

                cmd.Close();

                worker.CancelAsync();
            }
        }
    }
}
