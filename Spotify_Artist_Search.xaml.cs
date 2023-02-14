using SpotifyAPI.Web;
using System;
using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaction logic for Spotify_Artist_Search.xaml
    /// </summary>
    public partial class Spotify_Artist_Search : Window
    {
        public string artistID;

        public Spotify_Artist_Search()
        {
            InitializeComponent();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            txtsearchBox.Focus();
        }

        private void cmdSearch_Click(object sender, RoutedEventArgs e)
        {
            if (txtsearchBox.Text != string.Empty)
            {
                string searchtext = txtsearchBox.Text;

                string token = Spotify.GetAccessToken();

                if (token == "ConnectionFailed")
                {
                    MessageBox.Show("Connection failed. Please check your internet connection.", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                SpotifyClient spotifyClient = new(token);

                SearchRequest searchRequest = new(SearchRequest.Types.Artist, searchtext);
                var searchResponse = spotifyClient.Search.Item(searchRequest);
                var artist = searchResponse.Result.Artists.Items[0];
                artistID = artist.Id;

                MessageBoxResult result;
                result = MessageBox.Show("The following artist has been found:\n\n" + @"""" + artist.Name + @"""" +
                                "\n\n Do you confirm the search?", "Search completed", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    Close();
                else
                    return;
            }
        }
    }
}
