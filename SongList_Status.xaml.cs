using Org.BouncyCastle.Bcpg.OpenPgp;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

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
        Process cmd;
        XmlWriter xmlWriter;
        BackgroundWorker worker;
        bool _shown;
        bool downloadStarted;
        bool retryDownload;
        public bool allSongsDownloaded;
        string strCmdText;
        string downloadType;
        string extension;

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

                case "automatically select best audio format":
                    strCmdText = "yt-dlp -f bestaudio ";
                    break;

                case "automatically select best video format":
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
                            File.Delete(filePath);
                    }
                    catch (IOException) // file is still used by another process -> delete later
                    { }
                }

                cmd.Start();
                cmd.StandardInput.WriteLine(strCmdText + song.Link);
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();

                ReportCmdProgress(song, songIndex);

                cmd.WaitForExit();

                ChangeProperties(song);

                if (worker.CancellationPending == true) // window has been closed -> cancel backgroundworker
                {
                    e.Cancel = true;
                    return;
                }

                WriteToXmlFile(song);
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
                            File.Delete(filePath);
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
                    string subject = "YouTube-dl GUI => Failed Download";
                    string textBody = "<pre>" +
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
                    if (!downloadStarted)
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

            // access ffmpeg to edit metadata of file

            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = @"youtube-dl\";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;

            song.Artist = RemoveForbiddenCharacters(song.Artist);
            song.Songname = RemoveForbiddenCharacters(song.Songname);

            string strCmdText = "ffmpeg -i " + '"' + myFile.FullName + '"' + " -metadata title=" + '"' + song.Songname + '"' + " -metadata artist=" + '"' + song.Artist + '"' + " -metadata album=" + '"' + song.Album + '"' + " -metadata comment=" + '"' + song.Link + '"' + " -c copy " + '"' + myFile.FullName + '"';
            strCmdText = strCmdText.Insert(strCmdText.LastIndexOf('.'), "(2)");

            cmd.Start();
            cmd.StandardInput.WriteLine(strCmdText);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            cmd.WaitForExit();

            // ffmpeg can't overwrite existing files -> stored under new name -> myFile has to be assigned to new file
            myFile = new(myFile.FullName.Insert(myFile.FullName.LastIndexOf('.'), "(2)"));

            string songInfo = song.Artist + " - " + song.Songname;

            MoveFile(myFile, songInfo, "music/youtube-dl", 0);
        }

        public string RemoveForbiddenCharacters(string source)
        {
            // remove unwanted characters to be able to save the file
            char[] forbiddenCharacters = { '"', '*', '<', '>', '?', '\\', '|', '/', ':' };

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

        public void MoveFile(FileInfo file, string songInfo, string destination, int accountsOfFile)
        {
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
                        File.Delete(destination);
                        file.MoveTo(destination);
                    }
                }
                else
                {
                    if (destination == "music/youtube-dl")
                        file.MoveTo(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + @"\youtube-dl\" + songInfo + " (" + accountsOfFile + ')' + extension);
                    else
                    {
                        File.Delete(destination);
                        file.MoveTo(destination);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is DirectoryNotFoundException)
                {
                    Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + @"\youtube-dl");
                    MoveFile(file, songInfo, "music/youtube-dl", accountsOfFile);
                }
                else if (ex is IOException)
                {
                    accountsOfFile++;
                    MoveFile(file, songInfo, destination, accountsOfFile);
                }
            }
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
