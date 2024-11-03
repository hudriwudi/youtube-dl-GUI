using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Downloaded.xaml
    /// </summary>
    public partial class Downloaded : Window
    {
        SongList winSongList;
        Downloaded_ChangeSongInfo winChange;
        List<DownloadedSong> downloadedSongs;
        public List<Song> songList = new();

        public Downloaded(List<Song> songList)
        {
            InitializeComponent();

            this.songList = songList;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.PrimaryScreenWidth - 502;
            LoadXmlToDataGrid();
        }

        private void LoadXmlToDataGrid()
        {
            downloadedSongs = null;
            downloadedSongs = new List<DownloadedSong>();

            List<FileInfo> files = new DirectoryInfo("downloaded songs").GetFiles().OrderByDescending(f => f.Name).ToList();
            foreach (var file in files)
            {
                if (file.Length == 0) // empty file
                {
                    file.Delete();
                    continue;
                }

                XmlReader reader = XmlReader.Create(file.FullName);
                try { reader.ReadToFollowing("Song"); }
                catch (XmlException) // not readable file
                {
                    reader.Close();
                    file.Delete();
                    continue;
                }

                int i = 0;
                do
                {
                    bool abort = false;

                    DownloadedSong song = new();

                    XmlDocument doc = new();
                    doc.Load(file.FullName);
                    XmlNode topNode = doc.LastChild;

                    if (topNode.ChildNodes.Count > 0) // check wether file is empty
                    {
                        reader.ReadToFollowing("Name");
                        song.Songname = reader.ReadElementContentAsString();

                        reader.ReadToFollowing("Artist");
                        song.Artist = reader.ReadElementContentAsString();

                        reader.ReadToFollowing("Link");
                        song.Link = reader.ReadElementContentAsString();

                        reader.ReadToFollowing("Album");
                        song.Album = reader.ReadElementContentAsString();

                        reader.ReadToFollowing("Genres");
                        song.Genres = reader.ReadElementContentAsString();

                        song.XmlFilePath = file.FullName;
                        song.IndexInFile = i + 1; // not starting at index 0, but at 1
                        i++;

                        foreach (var item in downloadedSongs) // avoid duplicates
                        {
                            if (song.Artist == item.Artist && song.Songname == item.Songname && song.Link == item.Link)
                            {
                                abort = true;
                                break;
                            }
                        }

                        if (!abort)
                            downloadedSongs.Add(song);
                    }
                    else
                    {
                        reader.Close();
                        file.Delete();
                        break;
                    }
                }
                while (reader.ReadToFollowing("Song"));
            }
            datagridSongs.ItemsSource = null;
            datagridSongs.ItemsSource = downloadedSongs;
        }

        private void CmdAddSong_Click(object sender, RoutedEventArgs e)
        {
            if (datagridSongs.SelectedIndex != -1)
            {
                for (int i = datagridSongs.SelectedItems.Count - 1; i >= 0; i--)
                {
                    songList.Add((Song)datagridSongs.SelectedItems[i]);
                }


                if (winSongList != null)
                    winSongList.Close();

                winSongList = new(songList);
                winSongList.Owner = this;
                winSongList.Show();
            }
        }

        private void CmdChangeInfo_Click(object sender, RoutedEventArgs e)
        {
            // select file

            OpenFileDialog fileDialog = new();
            fileDialog.Filter = "Audio Files|*.mp3;*.wav;*.opus;*.m4a;*.mp4";
            fileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            fileDialog.ShowDialog();
            // open new window

            if (fileDialog.FileName != null)
            {
                string path = fileDialog.FileName;
                winChange = new Downloaded_ChangeSongInfo(path);
                winChange.ShowDialog();

                if (winChange.fileChanged)
                    MessageBox.Show("The file \"" + fileDialog.SafeFileName + "\" was successfully modified.", "Modification successfull", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CmdYT_Click(object sender, RoutedEventArgs e)
        {
            string link;
            int index = datagridSongs.SelectedIndex;
            Song chosenSong = (Song)datagridSongs.Items[index];
            if (chosenSong.Link != null)
                link = chosenSong.Link;
            else
                link = "https://www.youtube.com/watch?v=" + chosenSong.ID;

            // opens link in default browser
            ProcessStartInfo startInfo = new()
            {
                FileName = "cmd",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                Arguments = "/C start" + " " + link
            };
            Process.Start(startInfo);
        }

        private void cmdMP3_Click(object sender, RoutedEventArgs e)
        {
            if (!Youtube.IsConnectedToInternet())
            {
                MessageBox.Show("Could not connect to the Internet.\n\nPlease check whether you have a stable internet connection.", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // read in .mp3 files to download them again (making use of youtube search algorithm for better audio quality)

            Downloaded_SelectMP3_Status winStatus = new();
            winStatus.Owner = this;
            winStatus.ShowDialog();

            if (songList != null)
            {
                if (winSongList != null)
                    winSongList.Close();

                winSongList = new(songList);
                winSongList.Owner = this;
                winSongList.Show();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteFromXMLFile();
            }
        }

        private void cmdDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteFromXMLFile();
        }

        private void DeleteFromXMLFile()
        {
            if (datagridSongs.SelectedIndex != -1)
            {
                List<DownloadedSong> selectedSongs = new();

                for (int i = datagridSongs.SelectedItems.Count - 1; i >= 0; i--)
                {
                    selectedSongs.Add((DownloadedSong)datagridSongs.SelectedItems[i]);
                }

                foreach (var song in selectedSongs)
                {
                    string path = song.XmlFilePath;
                    XmlDocument xmlDoc = new();
                    xmlDoc.Load(path);

                    XmlNodeList nodeList = xmlDoc.SelectNodes("/Songs/Song/Name");
                    foreach (XmlNode node in nodeList)
                    {
                        if (node.InnerText == song.Songname)
                        {
                            XmlNode parentNode = node.ParentNode;
                            XmlNode topNode = parentNode.ParentNode;
                            topNode.RemoveChild(parentNode);
                            break;
                        }
                    }

                    string newPath = path.Insert(path.Length - 4, "(2)");
                    xmlDoc.Save(newPath);
                    File.Delete(path);
                    File.Move(newPath, path);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (winSongList != null)
                songList.AddRange(winSongList.songList.Except(songList)); // append song list without duplicates

            Youtube winYoutube = (Youtube)this.Owner;
            winYoutube.songList.AddRange(songList.Except(winYoutube.songList));

            try
            {
                this.Owner = null; // solves the minimizing of owner window after closing child window
            }
            catch (InvalidOperationException) { }
        }
    }
}
