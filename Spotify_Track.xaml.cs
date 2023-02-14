using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Spotify_Track.xaml
    /// </summary>
    public partial class Spotify_Track : Window
    {
        public string link;
        public bool returnToYTSearch;

        public Spotify_Track()
        {
            InitializeComponent();

            txtSongLink.Focus();
            txtSongLink.SelectAll();
        }

        private void CmdSearch_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result;
            result = MessageBox.Show("To search a track please use the Youtube search.\n\n Do you want to navigate to the Youtube search window?", "Feature not available", MessageBoxButton.YesNo, MessageBoxImage.Information); ;

            if (result == MessageBoxResult.Yes)
            {
                Youtube winYoutube = new();
                winYoutube.Show();
                returnToYTSearch = true;
                this.Hide();
            }
        }

        private void CmdDone_Click(object sender, RoutedEventArgs e)
        {
            link = txtSongLink.Text;
            if (link != null)
            {
                // test link = in right format

                // eg: https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT?si=d43d285f33dd4c74

                if (!link.Contains("spotify.com") && !link.Contains("track"))
                {
                    MessageBox.Show("Please enter a valid Link to a Spotify track.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            this.Hide();
        }
    }
}
