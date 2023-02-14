using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaction logic for Downloaded_SelectMP3_Status.xaml
    /// </summary>
    public partial class Downloaded_SelectMP3_Status : Window
    {
        Spotify winSpotify;
        List<Song> songList = new();
        string[] filePaths;
        bool _shown;

        public Downloaded_SelectMP3_Status()
        {
            InitializeComponent();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (_shown)
                return;
            _shown = true;

            winSpotify = new(null);

            // backgroundworker is used as to enable reporting progress to the label
            BackgroundWorker worker = new();
            worker.WorkerReportsProgress = true;
            worker.DoWork += new DoWorkEventHandler(Worker_DoWork);
            worker.ProgressChanged += new ProgressChangedEventHandler(Worker_ProgressChanged);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Worker_RunWorkerCompleted);

            if (!worker.IsBusy)
                worker.RunWorkerAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            worker.ReportProgress(0, "Accessing files...");

            OpenFileDialog fileDialog = new();
            fileDialog.Filter = "MP3 Audio Files|*.mp3";
            fileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            fileDialog.Multiselect = true;

            if (fileDialog.ShowDialog() == true)
            {
                worker.ReportProgress(0, "Reading in data...");
                Thread.Sleep(500); // display the message a bit longer for it to be readable

                filePaths = fileDialog.FileNames;
                int i = 0;

                foreach (string filePath in filePaths)
                {
                    TagLib.File file = TagLib.File.Create(filePath);
                    SpotifySong song = new SpotifySong();

                    song.Artist = string.Join("", file.Tag.Performers);
                    song.Songname = file.Tag.Title;

                    song.Spotify.Artist.Name = song.Artist;
                    song.Spotify.Songname = song.Songname;
                    song.Link = winSpotify.FindYoutubeVideo(song);

                    if (song.Link == string.Empty) // API quota limit reached
                        return;

                    songList.Add(song);

                    i++;
                    worker.ReportProgress(i, song.Artist + " - " + song.Songname);
                }
            }
            else
                Close();

            worker.ReportProgress(0, "All songs added.");
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblStatus.Content = (String)e.UserState;

            if (e.ProgressPercentage != 0)
                lblStatus.Content += "\n" + e.ProgressPercentage.ToString() + "/" + filePaths.Count() + " added";
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Thread.Sleep(1000);
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (songList != null)
            {
                Downloaded winDownloaded = (Downloaded)this.Owner;
                if (winDownloaded.songList == null)
                    winDownloaded.songList = songList;
                else
                    winDownloaded.songList.AddRange(songList);
            }
        }
    }
}
