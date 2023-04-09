using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Spotify_Link_Artist_TopOrAll.xaml
    /// </summary>
    public partial class Spotify_Artist_TopOrAll : Window
    {
        public string typeChosen;

        public Spotify_Artist_TopOrAll()
        {
            InitializeComponent();
        }

        private void CmdTop_Click(object sender, RoutedEventArgs e)
        {
            typeChosen = "top";
            this.Hide();
        }

        private void CmdAll_Click(object sender, RoutedEventArgs e)
        {
            typeChosen = "all";
            this.Hide();
        }
    }
}
