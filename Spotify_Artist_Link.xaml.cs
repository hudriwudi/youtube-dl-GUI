using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Spotify_Artist_Link.xaml
    /// </summary>
    public partial class Spotify_Artist_Link : Window
    {
        public string link;
        public bool artistWasSearched;

        public Spotify_Artist_Link()
        {
            InitializeComponent();

            txtArtistLink.Focus();
            txtArtistLink.SelectAll();
        }

        private void CmdDone_Click(object sender, RoutedEventArgs e)
        {
            link = txtArtistLink.Text;
            if (link != null)
            {
                // test link = in right format

                // eg: https://open.spotify.com/artist/0gxyHStUsqpMadRV0Di1Qt?si=Qqf55D6gT9SspTQpfb0g0A

                if (!link.Contains("spotify.com") && !link.Contains("artist"))
                {
                    MessageBox.Show("Please enter a valid Link to a Spotify artist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            this.Hide();
        }
    }
}
