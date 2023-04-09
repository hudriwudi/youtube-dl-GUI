using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für SongList_AddSong.xaml
    /// </summary>
    public partial class SongList_AddSong : Window
    {
        public Song newSong;

        public SongList_AddSong()
        {
            InitializeComponent();
            txtArtist.Focus();
            txtArtist.SelectAll();
        }

        private void CmdAdd_Click(object sender, RoutedEventArgs e)
        {
            newSong = new Song
            {
                Artist = txtArtist.Text,
                Songname = txtSongname.Text,
                Link = txtLink.Text
            };

            string link = newSong.Link;
            string id;

            int startIndex = link.IndexOf("?v=") + 3;
            int stopIndex = link.Length;
            if (startIndex == 2) // -1 + 3 -> "?v=" not found
            {
                MessageBox.Show("Unfortunately an invalid link was entered.\nPlease try again.", "Invalid link", MessageBoxButton.OK, MessageBoxImage.Error);
                txtLink.Text = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
                txtLink.Focus();
                txtLink.SelectAll();
                return;
            }
            id = link[startIndex..stopIndex];
            newSong.ID = id;

            if (link != ("https://www.youtube.com/watch?v=" + id))
                newSong.Link = "https://www.youtube.com/watch?v=" + id;

            SongList winSongList = (SongList)this.Owner;
            bool validLink = winSongList.CheckYTLink(id);

            if (validLink)
            {
                winSongList.songList.Add(newSong);
                Close();
            }
            else if (!winSongList.IsNotConnectedToInternet)
            {
                MessageBox.Show("Unfortunately an invalid link was entered.\nPlease try again.", "Invalid link", MessageBoxButton.OK, MessageBoxImage.Error);
                txtLink.Focus();
                txtLink.SelectAll();
            }
        }

        // *the following 3 functions are not my code*
        // https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox

        private void txtBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            TextBox txtBox = sender as TextBox;
            // Fixes issue when clicking cut/copy/paste in context menu
            if (txtBox.SelectionLength == 0)
                txtBox.SelectAll();
        }

        private void txtBox_LostMouseCapture(object sender, MouseEventArgs e)
        {
            TextBox txtBox = sender as TextBox;
            // If user highlights some text, don't override it
            if (txtBox.SelectionLength == 0)
                txtBox.SelectAll();

            // further clicks will not select all
            txtBox.LostMouseCapture -= txtBox_LostMouseCapture;
        }

        private void txtBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            TextBox txtBox = sender as TextBox;
            // once we've left the txtBox, return the select all behavior
            txtBox.LostMouseCapture += txtBox_LostMouseCapture;
        }
    }
}
