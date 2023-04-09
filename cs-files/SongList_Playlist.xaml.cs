using System.Collections.Generic;
using System.Configuration;
using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für SongList_Playlist.xaml
    /// </summary>
    public partial class SongList_Playlist : Window
    {
        public List<Song> playlistList = new();
        public List<Song> playlistSongList = new();
        public string link;

        public SongList_Playlist()
        {
            InitializeComponent();
            txtPlaylistLink.Focus();
            txtPlaylistLink.SelectAll();
        }

        private void CmdAdd_Click(object sender, RoutedEventArgs e)
        {
            string playlistLink = txtPlaylistLink.Text;
            int startIndex = playlistLink.IndexOf("list=") + 5;

            if (playlistLink.Contains("index="))
                playlistLink = playlistLink[0..playlistLink.IndexOf("&index=")];

            string playListID;
            if (startIndex != 4) // -1 + 5 = 4 -> "list=" not found
                playListID = playlistLink[startIndex..playlistLink.Length];
            else
            {
                MessageBox.Show("Please make sure to enter a valid youtube link.", "Link not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string webData = GetPlaylistWebData(playListID, null);

            if (webData.Contains("playlistNotFound"))
            {
                MessageBox.Show("Unfortunately the entered playlist could not be found.\n\n" +
                                @"Please make sure that the playlist isn't marked as ""private""." +
                             "\nIf it is, please change the privacy settings of the playlist to either " + @"""listed"" or ""public"".",
                             "Playlist not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool abort = false;
            int stopIndex;
            List<string> individualVideos = new();
            do
            {
                startIndex = webData.IndexOf("snippet");
                if (startIndex != -1)
                {
                    stopIndex = webData.IndexOf('}', webData.IndexOf("privacyStatus")) + 1;

                    individualVideos.Add(webData[0..stopIndex]);
                    webData = webData.Remove(0, stopIndex);
                }
                else
                    abort = true;
            }
            while (!abort);

            foreach (var item in individualVideos)
            {
                if (!item.Contains(@"privacyStatus"": ""privacyStatusUnspecified") && !item.Contains(@"title"": ""Deleted video"))
                {
                    Song newSong = new();

                    startIndex = item.IndexOf("videoId") + 11;
                    stopIndex = item.IndexOf(@"""", startIndex + 2);
                    newSong.ID = item[startIndex..stopIndex];

                    startIndex = item.IndexOf("title") + 9;
                    stopIndex = item.IndexOf("videoOwnerChannelTitle") - 12;
                    newSong.Songname = item[startIndex..stopIndex];

                    startIndex = item.IndexOf("videoOwnerChannelTitle") + 26;
                    stopIndex = item.IndexOf('"', startIndex);
                    newSong.Artist = item[startIndex..stopIndex];

                    playlistSongList.Add(newSong);
                }
            }

            Close();
        }

        private string GetPlaylistWebData(string playListID, string nextPageToken)
        {
            string apiKey = Youtube.DecryptText(ConfigurationManager.AppSettings["APIKey" + ((Youtube)Application.Current.MainWindow).APICredentialsIncrementor]);

            string pageToken = "";
            if (nextPageToken != null)
                pageToken = "&pageToken=" + nextPageToken;
            // max items per request = 50 -> nextPageToken is used to get all items of playlist

            string link1 = "https://youtube.googleapis.com/youtube/v3/playlistItems?part=contentDetails%2C%20status%2C%20snippet&maxResults=50&playlistId=";
            string link2 = "&fields=nextPageToken%2C%20prevPageToken%2C%20items(contentDetails%2FvideoId%2C%20status%2C%20snippet%2Ftitle%2C%20snippet%2FvideoOwnerChannelTitle)&key=";
            string fullLink = link1 + playListID + pageToken + link2 + apiKey;

            string webData = ((Youtube)Application.Current.MainWindow).GetWebData(fullLink, null);

            if (webData.Contains("error") && webData.Contains("quota")) // quota exceeded
            {
                ((Youtube)Application.Current.MainWindow).APICredentialsIncrementor++;
                GetPlaylistWebData(playListID, nextPageToken);
            }

            if (webData.Contains("nextPageToken"))
            {
                int startIndex = webData.IndexOf("nextPageToken") + 17;
                int stopIndex = webData.IndexOf('"', startIndex);
                string token = webData[startIndex..stopIndex];
                webData += GetPlaylistWebData(playListID, token); // append all results into one string
            }

            return webData;
        }
    }
}
