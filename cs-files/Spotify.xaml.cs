using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Spotify.xaml
    /// </summary>
    public partial class Spotify : Window
    {
        Youtube winYoutube;
        SongList winSongList;
        Downloaded winDownloads;
        Spotify_Status winStatus;
        SpotifyClient spotifyClient;
        public List<Song> songList = new();
        public bool unableToConnect = false;
        public bool? artistShouldBeSearched;
        public string spotifyLink;
        string accessToken;
        bool returnToYTSearch;
        bool artistWasSearched;

        public Spotify(List<Song> songList)
        {
            InitializeComponent();

            if (songList != null)
                this.songList = songList;

            winYoutube = ((Youtube)Application.Current.MainWindow);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            accessToken = GetAccessToken();
            if (accessToken == "Connection failed")
                unableToConnect = true;
            else
                spotifyClient = new SpotifyClient(accessToken);
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem selectedTreeView = (TreeViewItem)sender;

            switch (selectedTreeView.Header)
            {
                case "Youtube":
                    this.Hide();
                    winYoutube.ShowDialog();
                    break;

                case "Song list":
                    winSongList = new SongList(songList);
                    winSongList.Owner = this.Owner;
                    winSongList.ShowDialog();
                    break;

                case "Downloaded songs":
                    winDownloads = new Downloaded(songList);
                    winDownloads.Owner = this.Owner;
                    winDownloads.ShowDialog();
                    break;
            }
        }

        public static string GetAccessToken()
        {
            // https://stackoverflow.com/questions/34007228/how-to-call-the-spotify-api-from-c-sharp 
            // https://hendrikbulens.com/2015/01/07/c-and-the-spotify-web-api-part-i/ 
            // *this is partly not my code*

            string token;
            string url = "https://accounts.spotify.com/api/token";
            var clientid = Youtube.DecryptText(ConfigurationManager.AppSettings["SpotifyClientID"]);
            var clientsecret = Youtube.DecryptText(ConfigurationManager.AppSettings["SpotifyClientSecret"]);

            //request to get the access token
            var encode_clientid_clientsecret = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:{1}", clientid, clientsecret)));

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);

            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Accept = "application/json";
            webRequest.Headers.Add("Authorization: Basic " + encode_clientid_clientsecret);

            var request = ("grant_type=client_credentials");
            byte[] req_bytes = Encoding.ASCII.GetBytes(request);
            webRequest.ContentLength = req_bytes.Length;

            Stream strm = webRequest.GetRequestStream();
            strm.Write(req_bytes, 0, req_bytes.Length);
            strm.Close();

            HttpWebResponse resp;
            try { resp = (HttpWebResponse)webRequest.GetResponse(); }
            catch (System.Net.WebException)
            {
                MessageBox.Show("Could not connect to the Spotify API Services.\n\nPlease check whether you have a stable internet connection.", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return "ConnectionFailed";
            }
            String json = "";
            using (Stream respStr = resp.GetResponseStream())
            {
                using StreamReader rdr = new(respStr, Encoding.UTF8);
                json = rdr.ReadToEnd();
                rdr.Close();
            }

            int startIndex = json.IndexOf("access_token") + 15;
            int stopIndex = json.IndexOf("token_type") - 3;
            token = json[startIndex..stopIndex];

            return token;
        }

        public string FindYoutubeVideo(SpotifySong song)
        {
            // access youtube api and find a fitting youtube video for the song

            string searchtext = song.Spotify.Artist.Name + ' ' + song.Spotify.Songname + " audio";
            List<Song> searchResult = winYoutube.YoutubeSearch(searchtext, 5);

            if (searchResult == null) // API quota limit reached
                return string.Empty;

            int index = RankYoutubeSearch(searchResult, song.Spotify.DurationMS, song.Spotify.Artist.Name, true);
            return searchResult[index].Link;
        }

        public static int RankYoutubeSearch(List<Song> searchResult, double SongDurationMS, string artist, bool fromSpotify)
        {
            // create score for each result & rank the videos
            List<Song> ranking = new();

            foreach (var rankedsong in searchResult)
            {

                if (rankedsong.Channel.ToUpper().Contains("TOPIC"))
                    rankedsong.RankingScore += 25;

                if (rankedsong.Channel.ToUpper().Contains("OFFICIAL"))
                    rankedsong.RankingScore += 20;

                if (rankedsong.Channel.ToUpper().Contains("VEVO"))
                    rankedsong.RankingScore += 25;

                if (rankedsong.IsOfficialArtistChannel)
                    rankedsong.RankingScore += 25;

                string TITLE = rankedsong.Title.ToUpper();

                if (TITLE.Contains("MUSIC VIDEO") || TITLE.Contains("OFFICIAL VIDEO") || TITLE.Contains("(VIDEO)"))
                    rankedsong.RankingScore -= 35;

                if (TITLE.Contains("AUDIO"))
                    rankedsong.RankingScore += 10;

                if (TITLE.Contains("LYRIC"))
                    rankedsong.RankingScore += 4;

                if (TITLE.Contains("BEHIND THE SCENES"))
                    rankedsong.RankingScore -= 50;

                if (TITLE.Contains("REMASTER"))
                    rankedsong.RankingScore -= 5;

                if (TITLE.Contains("ALBUM"))
                    rankedsong.RankingScore -= 30;

                if (TITLE.Contains("REMIX"))
                    rankedsong.RankingScore -= 100;

                if (TITLE.Contains("VERSION"))
                    rankedsong.RankingScore -= 30;

                if (TITLE.Contains("RADIO"))
                    rankedsong.RankingScore -= 30;

                if (TITLE.Contains("LIVE"))
                    rankedsong.RankingScore -= 100;

                if (TITLE.Contains("PERFORM"))
                    rankedsong.RankingScore -= 100;

                if (TITLE.Contains("UNSENCORED"))
                    rankedsong.RankingScore += 15;

                if (TITLE.Contains("1 HOUR") || TITLE.Contains(" 1H ") || TITLE.Contains("(1H)"))
                    rankedsong.RankingScore -= 100;

                if (TITLE.Contains("COMPILATION"))
                    rankedsong.RankingScore -= 20;

                if (TITLE.Contains("TRAILER"))
                    rankedsong.RankingScore -= 25;


                // workaround for if statement with logical ORs -> threading errors with background worker (Spotify_Status) 
                string[] highQuality = { "HQ", "HIGH QUALITY", "HD", "HIGH DEFINITION" };
                bool abort = false;
                foreach (var highquality in highQuality)
                {
                    if (!abort)
                        if (TITLE.Contains(highquality))
                        {
                            rankedsong.RankingScore += 15;
                            abort = true;
                        }
                }

                string DESCRIPTION = rankedsong.Description.ToUpper();

                if (DESCRIPTION.Contains("LIVE"))
                    rankedsong.RankingScore -= 100;

                if (DESCRIPTION.Contains("BEHIND THE SCENES"))
                    rankedsong.RankingScore -= 20;

                if (DESCRIPTION.Contains("PERFORM"))
                    rankedsong.RankingScore -= 30;

                if (DESCRIPTION.Contains("REMIX"))
                    rankedsong.RankingScore -= 100;

                if (DESCRIPTION.Contains("VERSION"))
                    rankedsong.RankingScore -= 15;

                if (DESCRIPTION.Contains("GENERATED BY YOUTUBE"))
                    rankedsong.RankingScore += 20;

                if (DESCRIPTION.Contains("UNCENSORED"))
                    rankedsong.RankingScore += 15;

                string ARTIST = artist.ToUpper();
                if (TITLE.Contains(ARTIST) || DESCRIPTION.Contains(ARTIST) || rankedsong.Channel.ToUpper().Contains(ARTIST))
                    rankedsong.RankingScore += 5;

                // ranking generated by youtube
                int i = searchResult.Count;
                i--;
                if (i == 5)
                    rankedsong.RankingScore += (i * 4);
                else // i = 10
                    rankedsong.RankingScore += (i * 2);

                // filter out songs under 60s
                if (rankedsong.DurationMS < 60000)
                    rankedsong.RankingScore -= 1000;

                // time difference > 10s
                if (fromSpotify)
                {
                    double timeDifference = rankedsong.DurationMS - SongDurationMS;

                    // ensure that the difference is a positive number
                    if (timeDifference < 0)
                        timeDifference *= -1;

                    // for every second over 10s the score reduction is increased by 0
                    // 
                    if (timeDifference > 10000)
                        rankedsong.RankingScore -= (int)Math.Round((timeDifference - 10000) / 2000);
                }

                ranking.Add(rankedsong);
            }

            List<Song> rankingWithViewCount = ranking.OrderByDescending(x => x.ViewCount).ToList();

            // 3 top viewed videos get additional ranking score
            if (rankingWithViewCount.Count >= 3)
            {
                for (int i = 1; i <= 3; i++)
                {
                    rankingWithViewCount[i - 1].RankingScore += i * 4;
                }

            }

            Song chosenSong = rankingWithViewCount.MaxBy(x => x.RankingScore); // get best ranked song

            return ranking.IndexOf(chosenSong);
        }

        private async void CmdPlaylist_Click(object sender, RoutedEventArgs e)
        {
            //https://open.spotify.com/playlist/3dhFTqnuP0ZHeG2Qtk3mTV?si=bcb01182c7504ad0
            await AskForLink("playlist");

            if (spotifyLink == null)
                return;

            string id = ExtractIDfromLink(spotifyLink);

            // check for invalid link
            try
            { var playlist = await spotifyClient.Playlists.Get(id); }
            catch (SpotifyAPI.Web.APIException exception)
            {
                if (exception.ToString().Contains("Service unavailable"))
                {
                    MessageBox.Show("Unfortunately the Spotify Services are currently unavailable.\n\nThis is a temporary condition on server side. Please try again in a few hours.",
                                    "Error: Spotify Servers unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    MessageBox.Show("Invalid link - please enter a valid spotify link.", "Error: Invalid ID", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            winStatus = new("Playlist", id, spotifyClient, this);
            winStatus.ShowDialog();

            OpenWinSongList();
        }

        private async void CmdTrack_Click(object sender, RoutedEventArgs e)
        {
            // https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT?si=d43d285f33dd4c74
            await AskForLink("track");

            if (returnToYTSearch || spotifyLink == null)
                return;

            string id = ExtractIDfromLink(spotifyLink);

            try
            {
                var test = spotifyClient.Tracks.Get(id);
                var test2 = test.Result.Id;
            }
            catch (Exception exception)
            {
                if (exception is SpotifyAPI.Web.APIException || exception is AggregateException)
                {
                    if (exception.ToString().Contains("Service unavailable"))
                    {
                        MessageBox.Show("Unfortunately the Spotify Services are currently unavailable.\n\nThis is a temporary condition on server side. Please try again in a few hours.",
                                        "Error: Spotify Servers unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    else
                    {
                        MessageBox.Show("Invalid link - please enter a valid spotify link.", "Error: Invalid ID", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                    throw;
            }

            winStatus = new("Track", id, spotifyClient, this);
            winStatus.ShowDialog();

            OpenWinSongList();
        }

        private async void CmdArtist_Click(object sender, RoutedEventArgs e)
        {
            // https://open.spotify.com/artist/0gxyHStUsqpMadRV0Di1Qt?si=Qqf55D6gT9SspTQpfb0g0A
            await AskForLink("artist");

            if (spotifyLink == null)
                return;

            string id;

            if (artistWasSearched)
                id = spotifyLink;
            else
                id = ExtractIDfromLink(spotifyLink);

            // check for invalid link
            try
            { var artist = await spotifyClient.Artists.Get(id); }
            catch (SpotifyAPI.Web.APIException exception)
            {
                if (exception.ToString().Contains("Service unavailable"))
                {
                    MessageBox.Show("Unfortunately the Spotify Services are currently unavailable.\n\nThis is a temporary condition on server side. Please try again in a few hours.",
                                    "Error: Spotify Servers unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    MessageBox.Show("Invalid link - please enter a valid spotify link.", "Error: Invalid ID", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // ask all or only top tracks
            Spotify_Artist_TopOrAll winSpotifyLinkArtistTopOrAll = new();
            winSpotifyLinkArtistTopOrAll.ShowDialog();

            bool allTracks;
            if (winSpotifyLinkArtistTopOrAll.typeChosen == "all")
                allTracks = true;
            else if (winSpotifyLinkArtistTopOrAll.typeChosen == "top")
                allTracks = false;
            else // window was closed
                return;

            switch (allTracks)
            {
                // get all tracks
                case true:
                    winStatus = new Spotify_Status("ArtistALL", id, spotifyClient, this);
                    break;

                // get top tracks
                case false:
                    winStatus = new Spotify_Status("ArtistTOP", id, spotifyClient, this);
                    break;
            }
            winStatus.ShowDialog();

            OpenWinSongList();
        }

        private Task AskForLink(string type)
        {
            switch (type)
            {
                case "playlist":
                    Spotify_Playlist winSpotifyPlaylist = new();
                    winSpotifyPlaylist.Owner = this;
                    winSpotifyPlaylist.ShowDialog();
                    spotifyLink = winSpotifyPlaylist.link;
                    break;

                case "track":
                    Spotify_Track winSpotifyTrack = new();
                    winSpotifyTrack.Owner = this;
                    winSpotifyTrack.ShowDialog();
                    returnToYTSearch = winSpotifyTrack.returnToYTSearch;
                    spotifyLink = winSpotifyTrack.link;
                    if (returnToYTSearch)
                        this.Hide();
                    break;

                case "artist":
                    Spotify_Artist_SearchOrLink winSearchOrLink = new();
                    winSearchOrLink.Owner = this;
                    winSearchOrLink.ShowDialog();
                    if (artistShouldBeSearched.HasValue) // bool was set -> winSearchOrLink wasn't closed without pressing button
                    {
                        if (artistShouldBeSearched.Value)
                        {
                            Spotify_Artist_Search winArtistSearch = new();
                            winArtistSearch.Owner = this;
                            winArtistSearch.ShowDialog();
                            spotifyLink = winArtistSearch.artistID;
                            artistWasSearched = true;
                        }
                        else // link is used
                        {
                            Spotify_Artist_Link winSpotifyArtist = new();
                            winSpotifyArtist.Owner = this;
                            winSpotifyArtist.ShowDialog();
                            spotifyLink = winSpotifyArtist.link;
                        }
                    }
                    break;
            }

            return Task.CompletedTask;
        }

        private static string ExtractIDfromLink(string link)
        {
            // eg: https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT?si=d43d285f33dd4c74

            int startIndex = link.LastIndexOf('/') + 1;
            int stopIndex = link.Length;
            if (link.Contains("?si="))
                stopIndex = link.IndexOf("?si=");

            string ID = link[startIndex..stopIndex];

            return ID;
        }

        private void OpenWinSongList()
        {
            if (winSongList != null)
                winSongList.Close();

            winSongList = new SongList(songList);
            winSongList.Owner = this;
            winSongList.ShowDialog();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Youtube winYoutube = (Youtube)this.Owner;
            winYoutube.songList.AddRange(songList.Except(winYoutube.songList));
            winYoutube.Show();

            base.OnClosing(e);
        }
    }
}
