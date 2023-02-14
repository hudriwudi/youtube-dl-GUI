using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Spotify_Link_Playlist.xaml
    /// </summary>
    public partial class Spotify_Playlist : Window
    {
        public string link;

        public Spotify_Playlist()
        {
            InitializeComponent();

            txtPlaylistLink.Focus();
            txtPlaylistLink.SelectAll();
        }

        private void CmdDone_Click(object sender, RoutedEventArgs e)
        {
            link = txtPlaylistLink.Text;
            if (link != null)
            {
                // test link = in right format

                // eg: https://open.spotify.com/playlist/3dhFTqnuP0ZHeG2Qtk3mTV?si=bcb01182c7504ad0

                if (!link.Contains("spotify.com") && !link.Contains("playlist"))
                {
                    MessageBox.Show("Please enter a valid Link to a Spotify playlist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            this.Hide();
        }
    }
}
