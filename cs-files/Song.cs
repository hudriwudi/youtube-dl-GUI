namespace youtube_dl_v2
{
    public partial class Song
    {
        public string Album { get; set; }
        public string Artist { get; set; }
        public string Channel { get; set; }
        public string ChannelId { get; set; }
        public string Description { get; set; }
        public string Duration { get; set; }
        public double DurationMS { get; set; }
        public string Genres { get; set; }
        public string ID { get; set; }
        public bool IsOfficialArtistChannel { get; set; }
        public string Link { get; set; }
        public int RankingScore { get; set; }
        public string Songname { get; set; }
        public string ThumbnailSource { get; set; }
        public string Title { get; set; }
        public double ViewCount { get; set; }
    }

    public class DownloadedSong : Song
    {
        public int IndexInFile { get; set; }
        public string XmlFilePath { get; set; }
    }

    public class SpotifySong : Song
    {
        public class SpotifyAttributes
        {
            public double DurationMS { get; set; }
            public string Id { get; set; }
            public string Songname { get; set; }

            public class SpotifyArtist
            {
                public string Id { get; set; }
                public string Name { get; set; }
            }

            public class SpotifyAlbum
            {
                public string Name { get; set; }
                public string ImageLink { get; set; }
                public string Release_date { get; set; }
                public int Total_tracks { get; set; }
                public string Uri { get; set; }
            }

            public SpotifyAlbum Album = new SpotifyAlbum();
            public SpotifyArtist Artist = new SpotifyArtist();
        }

        public SpotifyAttributes Spotify = new SpotifyAttributes();
    }
}
