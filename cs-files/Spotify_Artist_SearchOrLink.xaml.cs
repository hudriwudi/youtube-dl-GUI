using System;
using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaction logic for Spotify_Artist_SearchOrLink.xaml
    /// </summary>
    public partial class Spotify_Artist_SearchOrLink : Window
    {
        Spotify winSpotify;
        bool buttonWasPressed;

        public Spotify_Artist_SearchOrLink()
        {
            InitializeComponent();
        }

        private void cmdSearch_Click(object sender, RoutedEventArgs e)
        {
            winSpotify = (Spotify)this.Owner;
            winSpotify.artistShouldBeSearched = true;
            buttonWasPressed = true;
            this.Close();
        }

        private void cmdLink_Click(object sender, RoutedEventArgs e)
        {
            winSpotify = (Spotify)this.Owner;
            winSpotify.artistShouldBeSearched = false;
            buttonWasPressed = true;
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (!buttonWasPressed)
            {
                winSpotify = (Spotify)this.Owner;
                winSpotify.artistShouldBeSearched = null;
            }
        }
    }
}
