using ATL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Downloaded_ChangeSongInfo.xaml
    /// </summary>
    public partial class Downloaded_ChangeSongInfo : Window
    {
        public DownloadedSong song = new();
        Track track;
        List<string> genres;
        public bool fileChanged;
        string filePath;

        public Downloaded_ChangeSongInfo(string filePath)
        {
            InitializeComponent();
            this.filePath = filePath;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // read in metadata

            track = new(filePath);

            song.Songname = track.Title;
            song.Artist = track.Artist;
            song.Album = track.Album;
            song.Genres = track.Genre;
            song.Link = track.Comment;

            txtSongname.Text = song.Songname;
            txtArtist.Text = song.Artist;
            txtAlbum.Text = song.Album;
            txtLink.Text = song.Link;

            if (song.Genres != null)
            {
                genres = new();
                int index;
                do
                {
                    index = song.Genres.IndexOf(';');
                    if (index != -1)
                    {
                        string genre = song.Genres[0..index];
                        genres.Add(genre);
                        song.Genres = song.Genres.Remove(0, index + 1);
                    }
                }
                while (index != -1);

                try
                {
                    txtGenres_1.Text = genres[0];
                    txtGenres_2.Text = genres[1];
                    txtGenres_3.Text = genres[2];
                }
                catch (ArgumentOutOfRangeException) { }
            }


            txtArtist.Focus();
            txtArtist.SelectAll();
        }

        private void CmdChange_Click(object sender, RoutedEventArgs e)
        {
            // modify metadata

            track.Title = txtSongname.Text;
            track.Artist = txtArtist.Text;
            track.Album = txtAlbum.Text;
            track.Comment = txtLink.Text;

            // combine genres to a single string
            genres = new();
            genres.AddRange(new string[] { txtGenres_1.Text, txtGenres_2.Text, txtGenres_3.Text });

            string strGenres = "";
            foreach (string genre in genres)
            {
                if (genre != string.Empty)
                {
                    strGenres += genre + ';';
                }
            }

            track.Genre = strGenres;
            track.Save();

            // rename file
            string fileDir = filePath.Remove(filePath.LastIndexOf('\\'));
            string fileName = track.Artist + " - " + track.Title;
            string fileExtension = Path.GetExtension(filePath);
            string newFilePath = fileDir + '\\' + fileName + fileExtension;
            File.Move(filePath, newFilePath);

            fileChanged = true;
            Close();
        }

        private void CmdReset_Click(object sender, RoutedEventArgs e)
        {
            txtArtist.Text = song.Artist;
            txtSongname.Text = song.Songname;
            txtAlbum.Text = song.Album;
            txtLink.Text = song.Link;

            try
            {
                txtGenres_1.Text = genres[0];
                txtGenres_2.Text = genres[1];
                txtGenres_3.Text = genres[2];
            }
            catch (ArgumentOutOfRangeException) { }
        }

        private void CmdCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
