using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaction logic for Spotify_Status.xaml
    /// </summary>
    public partial class Spotify_Status : Window
    {
        Spotify winSpotify;
        SpotifyClient spotifyClient;
        BackgroundWorker worker;
        List<Song> songList = new();
        string type;
        string id;
        bool _shown;

        public Spotify_Status(string type, string id, SpotifyClient spotifyClient, Spotify winSpotify)
        {
            InitializeComponent();
            this.type = type;
            this.id = id;
            this.spotifyClient = spotifyClient;
            this.winSpotify = winSpotify;

            lblStatus.Content = "Accessing Spotify API...";
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (_shown)
                return;
            _shown = true;


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
            worker = (BackgroundWorker)sender;

            worker.ReportProgress(1, "Accessing Spotify API...");

            switch (type)
            {
                case "Track":
                    DownloadTrack();
                    break;

                case "Playlist":
                    DownloadPlaylist().Wait();
                    break;

                case "ArtistTOP":
                    DownloadArtistTOP().Wait();
                    break;

                case "ArtistALL":
                    DownloadArtistALL().Wait();
                    break;
            }

            worker.ReportProgress(1, "All tracks have been added.");
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblStatus.Content = (String)e.UserState;

            string track_s = "tracks";
            if (songList.Count == 1)
                track_s = "track";

            if (e.ProgressPercentage == 0)
                lblStatus.Content += "\n" + songList.Count + " " + track_s + " added";
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Thread.Sleep(1000);
            Close();
        }

        private void DownloadTrack()
        {
            var spotifySong = new SpotifySong();
            var newsong = new SpotifySong();
            Task<FullTrack> track = spotifyClient.Tracks.Get(id);

            spotifySong.Spotify.Id = track.Result.Id;
            spotifySong.Spotify.Songname = track.Result.Name;
            spotifySong.Spotify.Artist.Name = track.Result.Artists[0].Name;
            spotifySong.Spotify.Artist.Id = track.Result.Artists[0].Id;
            spotifySong.Spotify.Album.ImageLink = track.Result.Album.Images[0].Url;
            spotifySong.Spotify.DurationMS = track.Result.DurationMs;

            newsong.Artist = spotifySong.Spotify.Artist.Name;
            newsong.Songname = spotifySong.Spotify.Songname;
            newsong.Album = track.Result.Album.Name;
            newsong.Genres = SongList_Status.AddGenres(spotifyClient.Artists.Get(spotifySong.Spotify.Artist.Id).Result.Genres.ToArray());
            newsong.Link = winSpotify.FindYoutubeVideo(spotifySong);

            if (newsong.Link == string.Empty) // API quota limit reached
                return;

            songList.Add(newsong);

            worker.ReportProgress(0, newsong.Artist + " - " + newsong.Songname);
        }

        private async Task DownloadArtistALL()
        {
            List<List<SimpleTrack>> songsOfArtist = new();
            Paging<SimpleAlbum> albumsOfArtist = await spotifyClient.Artists.GetAlbums(id);

            foreach (var album in albumsOfArtist.Items)
            {
                var songsOfAlbum = await spotifyClient.Albums.GetTracks(album.Id);
                songsOfArtist.Add(songsOfAlbum.Items);
            }

            foreach (var album in songsOfArtist)
            {
                foreach (var song in album)
                {
                    var spotifySong = new SpotifySong();

                    spotifySong.Spotify.Id = song.Id;
                    spotifySong.Spotify.Songname = song.Name;
                    spotifySong.Spotify.Artist.Name = song.Artists[0].Name;
                    spotifySong.Spotify.Artist.Id = song.Artists[0].Id;
                    spotifySong.Spotify.DurationMS = song.DurationMs;

                    var newsong = new Song();
                    newsong.Artist = song.Artists[0].Name;
                    newsong.Songname = song.Name;
                    newsong.Link = winSpotify.FindYoutubeVideo(spotifySong);

                    if (newsong.Link == string.Empty) // API quota limit reached
                        return;

                    songList.Add(newsong);

                    worker.ReportProgress(0, newsong.Artist + " - " + newsong.Songname);
                }
            }
        }

        private async Task DownloadArtistTOP()
        {
            // get top tracks

            var request = new ArtistsTopTracksRequest("AT");
            ArtistsTopTracksResponse toptracks = await spotifyClient.Artists.GetTopTracks(id, request);

            foreach (var track in toptracks.Tracks)
            {
                var spotifySong = new SpotifySong();
                var newSong = new SpotifySong();

                spotifySong.Spotify.Id = track.Id;
                spotifySong.Spotify.Songname = track.Name;
                spotifySong.Spotify.Artist.Name = track.Artists[0].Name;
                spotifySong.Spotify.Artist.Id = track.Artists[0].Id;
                spotifySong.Spotify.Album.ImageLink = track.Album.Images[0].Url;
                spotifySong.Spotify.DurationMS = track.DurationMs;

                newSong.Artist = spotifySong.Spotify.Artist.Name;
                newSong.Songname = spotifySong.Spotify.Songname;
                newSong.Album = track.Album.Name;
                newSong.Genres = SongList_Status.AddGenres(spotifyClient.Artists.Get(id).Result.Genres.ToArray());
                newSong.Link = winSpotify.FindYoutubeVideo(spotifySong);

                if (newSong.Link == string.Empty) // API quota limit reached
                    return;

                songList.Add(newSong);

                worker.ReportProgress(0, newSong.Artist + " - " + newSong.Songname);
            }
        }

        private async Task DownloadPlaylist()
        {
            List<SpotifySong> playlistSongs = new();

            await GetPlaylistItems(id, playlistSongs);

            foreach (var playlistsong in playlistSongs)
            {
                var newsong = new Song();
                newsong.Artist = playlistsong.Spotify.Artist.Name;
                newsong.Songname = playlistsong.Spotify.Songname;
                newsong.Link = winSpotify.FindYoutubeVideo(playlistsong);

                if (newsong.Link == string.Empty) // API quota limit reached
                    return;

                songList.Add(newsong);

                worker.ReportProgress(0, newsong.Artist + " - " + newsong.Songname);
            }
        }

        public async Task GetPlaylistItems(string id, List<SpotifySong> spotifyPlaylist)
        {
            // https://stackoverflow.com/questions/63483713/spotify-api-client-get-playlist-tracks-offset
            // https://johnnycrazy.github.io/SpotifyAPI-NET/docs/getting_started/

            FullPlaylist playlist = await spotifyClient.Playlists.Get(id);

            foreach (PlaylistTrack<IPlayableItem> item in playlist.Tracks.Items)
            {
                SpotifySong newSong = new();

                if (item.Track is FullTrack track)
                {
                    newSong.Spotify.Id = track.Id;
                    newSong.Spotify.Songname = track.Name;
                    newSong.Spotify.Artist.Name = track.Artists[0].Name;
                    newSong.Spotify.Artist.Id = track.Artists[0].Id;
                    newSong.Spotify.Album.Name = track.Album.Name;
                    newSong.Spotify.Album.ImageLink = track.Album.Images[0].Url;
                    newSong.Spotify.DurationMS = track.DurationMs;

                    spotifyPlaylist.Add(newSong);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (songList != null)
            {
                if (winSpotify.songList == null)
                    winSpotify.songList = songList;
                else
                    winSpotify.songList.AddRange(songList);
            }
        }
    }
}
