using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Web;
using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für SongList.xaml
    /// </summary>
    public partial class SongList : Window
    {
        public SongList_Status winStatus;
        public List<Song> songList = new();
        public bool IsNotConnectedToInternet;
        string downloadType = ".mp3";

        public SongList(List<Song> songList)
        {
            InitializeComponent();
            this.songList = songList;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            datagridSongs.ItemsSource = this.songList;
        }

        private void CmdRemove_Click(object sender, RoutedEventArgs e)
        {
            if (datagridSongs.SelectedIndex != -1)
            {
                for (int i = datagridSongs.SelectedItems.Count - 1; i >= 0; i--)
                {
                    songList.Remove((Song)datagridSongs.SelectedItems[i]);
                }

                UpdateDatagrid();
            }
        }

        private void CmdRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            songList.Clear();
            UpdateDatagrid();
        }

        private void CmdDownloadAll_Click(object sender, RoutedEventArgs e)
        {
            if (songList.Count != 0)
                DownloadSongs(songList);
        }

        private void cmdDownload_Click(object sender, RoutedEventArgs e)
        {
            if (datagridSongs.SelectedIndex != -1)
            {
                List<Song> selectedSongs = new();

                for (int i = datagridSongs.SelectedItems.Count - 1; i >= 0; i--)
                {
                    selectedSongs.Add((Song)datagridSongs.SelectedItems[i]);
                }

                DownloadSongs(selectedSongs);
            }
        }

        private void DownloadSongs(List<Song> songs)
        {
            if (!Youtube.IsConnectedToInternet())
            {
                MessageBox.Show("Could not connect to the Internet.\n\nPlease check whether you have a stable internet connection.", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            winStatus = new SongList_Status(songs, downloadType);
            winStatus.Owner = this;
            winStatus.ShowDialog();

            if (winStatus.allSongsDownloaded)
                MessageBox.Show("All songs have been downloaded.", "Download completed", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Unfortunately the download was aborted.", "Download aborted", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CmdUpload_Click(object sender, RoutedEventArgs e)
        {
            string line;
            bool unreadableFormat = false;
            List<Song> localSongList = new();

            MessageBox.Show(@"The content of the uploaded .txt file has to be in the following format: ""link artist - title""" +
                           "\neg:\nhttps://www.youtube.com/watch?v=dQw4w9WgXcQ Rick Astley - Never Gonna Give You Up" + "" +
                           "\n\nOther formats will not be accepted.", "Information",
                           MessageBoxButton.OK, MessageBoxImage.Information);

            OpenFileDialog fileDialog = new();
            fileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            fileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

            if (fileDialog.ShowDialog() == true)
            {
                StreamReader reader = new(fileDialog.FileName);

                // format :     link artist - song
                //     eg :     https://www.youtube.com/watch?v=dQw4w9WgXcQ Rick Astley - Never Gonna Give You Up

                while ((line = reader.ReadLine()) != null)
                {
                    try
                    {
                        Song newSong = new();

                        int positionSpace = line.IndexOf(' ');
                        newSong.Link = line[0..positionSpace];

                        int positionMinus = line.LastIndexOf('-') - 1;
                        newSong.Artist = line[(positionSpace + 1)..positionMinus];

                        newSong.Songname = line[(positionMinus + 3)..line.Length];

                        if (!newSong.Link.Contains("youtube.com"))
                            unreadableFormat = true;
                        else
                            localSongList.Add(newSong);
                    }
                    catch (ArgumentOutOfRangeException)
                    { } // prevent empty lines causing an error
                }
                if (unreadableFormat)
                    MessageBox.Show("Unfortunately the text file was in the wrong format. Please make sure that every video is in the following format:\n\n" +
                                 @"""link artist - title""" + "\n\n eg:\nhttps://www.youtube.com/watch?v=dQw4w9WgXcQ Rick Astley - Never Gonna Give You Up",
                                    "Wrong Format", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                {
                    songList.AddRange(localSongList);
                    UpdateDatagrid();
                }
            }
        }

        private void CmdPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (!Youtube.IsConnectedToInternet())
            {
                MessageBox.Show("Could not connect to the Internet.\n\nPlease check whether you have a stable internet connection.", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SongList_Playlist winPlaylist = new();
            winPlaylist.Owner = this;
            winPlaylist.ShowDialog();

            foreach (var item in winPlaylist.playlistSongList)
            {
                var song = item;
                if (item.Artist.Contains("- Topic"))
                    song.Artist = song.Artist[0..song.Artist.IndexOf(" - Topic")];

                songList.Add(song);
            }
            UpdateDatagrid();
        }

        private void CmdAddSong_Click(object sender, RoutedEventArgs e)
        {
            SongList_AddSong winSongList_AddSong = new();
            winSongList_AddSong.Owner = this;
            winSongList_AddSong.ShowDialog();




            UpdateDatagrid();
        }

        public bool CheckYTLink(string id)
        {
            // check wether it's a valid yt link by calling API

            string apiKey = Youtube.DecryptText(ConfigurationManager.AppSettings["APIKey" + ((Youtube)Application.Current.MainWindow).APICredentialsIncrementor]);
            string webData;
            try
            {
                string link = "https://youtube.googleapis.com/youtube/v3/videos?part=contentDetails&id=" + id + "&fields=items(contentDetails%2Fduration)&key=" + apiKey;
                HttpClient client = new();
                var request = new HttpRequestMessage(HttpMethod.Get, link);
                var response = client.Send(request);
                var reader = new StreamReader(response.Content.ReadAsStream());
                webData = reader.ReadToEnd();
                webData = HttpUtility.HtmlDecode(webData);
            }
            catch (HttpRequestException)
            {
                MessageBox.Show("Could not connect to the Internet.\n\nPlease check whether you have a stable internet connection.", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
                IsNotConnectedToInternet = true;
                return false;
            }

            if (webData.Contains("quotaExceeded"))
            {
                if (((Youtube)Application.Current.MainWindow).APICredentialsIncrementor < 30)
                {
                    ((Youtube)Application.Current.MainWindow).APICredentialsIncrementor++;
                    return CheckYTLink(id);
                }
                else
                    MessageBox.Show("Unfortunately the daily limit of 3000 requests to the Youtube Data API has been reached. Please try it again tomorrow. If the problem keeps coming up, please don't hesitate contacting the developer.", "Error: Limit Reached", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (webData.Contains(@"items"": []")) // no items found -> video does not exist
                return false;
            else
                return true;
        }

        private void CmdYT_Click(object sender, RoutedEventArgs e)
        {
            string link;
            int index = datagridSongs.SelectedIndex;
            Song chosenSong = (Song)datagridSongs.Items[index];
            if (chosenSong.Link != null)
                link = chosenSong.Link;
            else
                link = "https://www.youtube.com/watch?v=" + chosenSong.ID;

            Process process = new();
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.FileName = "chrome.exe";
            process.StartInfo.Arguments = link;
            process.Start();
        }

        private void UpdateDatagrid()
        {
            datagridSongs.ItemsSource = null;
            datagridSongs.ItemsSource = songList;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // keep the songlist always synchronised
            if (songList != null)
            {
                foreach (var window in App.Current.Windows)
                {
                    var type = window.GetType();

                    switch (type.Name)
                    {
                        case "Youtube":
                            var winYoutube = (Youtube)window;
                            winYoutube.songList = songList;
                            break;

                        case "Spotify":
                            var winSpotify = (Spotify)window;
                            winSpotify.songList = songList;
                            break;

                        case "Downloaded":
                            var winDownloaded = (Downloaded)window;
                            winDownloaded.songList = songList;
                            break;
                    }
                }
                try
                {
                    this.Owner = null; // solves the minimizing of owner window after closing child window
                }
                catch (InvalidOperationException) { }
            }
        }

        private void cbxType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            downloadType = cbxType.SelectedValue.ToString();
        }
    }
}
