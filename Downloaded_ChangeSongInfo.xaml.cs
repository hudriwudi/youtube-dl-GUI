using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TagLib.Id3v2;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Downloaded_ChangeSongInfo.xaml
    /// </summary>
    public partial class Downloaded_ChangeSongInfo : Window
    {
        public DownloadedSong song;
        public bool fileChanged;
        string filePath;

        public Downloaded_ChangeSongInfo(DownloadedSong song, string filePath)
        {
            InitializeComponent();
            this.song = song;
            this.filePath = filePath;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtArtist.Text = song.Artist;
            txtSongname.Text = song.Songname;
            txtLink.Text = song.Link;

            txtArtist.Focus();
            txtArtist.SelectAll();
        }

        private void CmdChange_Click(object sender, RoutedEventArgs e)
        {
            // code adapted from SongList_Status.xaml.cs

            song.Artist = txtArtist.Text;
            song.Songname = txtSongname.Text;
            song.Link = txtLink.Text;

            var myFile = new FileInfo(filePath);

            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = @"youtube-dl\";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;

            SongList_Status winSongList_Status = new(null, null);
            song.Artist = winSongList_Status.RemoveForbiddenCharacters(song.Artist);
            song.Songname = winSongList_Status.RemoveForbiddenCharacters(song.Songname);

            string songInfo = song.Artist + " - " + song.Songname;
            string newFileName = myFile.DirectoryName + @"\" + songInfo + myFile.Extension;

            string strCmdText = "ffmpeg -i " + '"' + myFile.FullName + '"' + " -metadata title=" + '"' + song.Songname + '"' + " -metadata artist=" + '"' + song.Artist + '"' + " -metadata album=" + '"' + song.Album + '"' + " -metadata comment=" + '"' + song.Link + '"' + " -c copy " + '"' + newFileName + '"';
            strCmdText = strCmdText.Insert(strCmdText.LastIndexOf("."), "(2)");

            cmd.Start();
            cmd.StandardInput.WriteLine(strCmdText);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            cmd.WaitForExit();

            // ffmpeg can't overwrite existing files -> stored under new name -> myFile has to be assigned to new file
            string tempFileName = newFileName.Insert(newFileName.LastIndexOf("."), "(2)");
            myFile = new(tempFileName);

            winSongList_Status.MoveFile(myFile, songInfo, newFileName, 0);

            File.Delete(filePath); // delete original file

            Close();
        }

        private void CmdReset_Click(object sender, RoutedEventArgs e)
        {
            txtArtist.Text = song.Artist;
            txtSongname.Text = song.Songname;
            txtLink.Text = song.Link;
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
