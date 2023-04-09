using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;
using MessageBox = System.Windows.MessageBox;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaction logic for Youtube.xaml
    /// </summary>
    public partial class Youtube : Window
    {
        // Kristoferitsch Daniel
        // GUI for YouTube-dl with additionial features

        string searchtext;
        string artist, songname;
        public List<Song> songList = new();
        public List<Song> searchList = new();
        SongList winSongList;
        Spotify winSpotify;
        Downloaded winDownloads;
        public int APICredentialsIncrementor = 1;
        int attemptCounter = 0;
        bool quotaExceeded;

        public Youtube()
        {
            InitializeComponent();

            this.Icon = new BitmapImage(new Uri("yt-dl-logo.ico", UriKind.Relative));
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            artist = "Rick Astley";
            songname = "Never Gonna Give You Up";
            UpdateSearchBar();
            txtArtist.Text = artist;
            txtSongname.Text = songname;
            txtArtist.UpdateLayout();
            txtSongname.UpdateLayout();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // notify developer that the download was completed

            string path = Environment.CurrentDirectory + @"\Installation completed.txt";
            if (!File.Exists(path) && Environment.UserName != "Daniel")
            {
                string subject = "YouTube-dl GUI => Installation completed";
                string textBody = "<pre>" +
                                  "This email notifies of the successful completion of an installation." +
                              "\n\nVersion: " + Assembly.GetExecutingAssembly().GetName().Version.ToString() +
                                "\nUser: " + Environment.UserName +
                                  "<pre>";
                App.SendEmail(subject, textBody);

                File.WriteAllText(path, "This file exists to check whether the installation was completed.");
            }

            // check whether there is a newer version available

            if (IsConnectedToInternet())
            {
                string currentVersion = "v1.0.3"; // change when releasing new version
                string newestVersion = null;

                AuthenticationHeaderValue header = new("User-Agent", "hudriwudi");
                string data = GetWebData("https://api.github.com/repos/hudriwudi/youtube-dl-GUI/tags", header);

                if (!data.Contains("Request forbidden"))
                {
                    newestVersion = data[(data.IndexOf("name") + 7)..(data.IndexOf("zipball_url") - 3)];

                    if (currentVersion != newestVersion)
                    {
                        MessageBoxResult result =
                        MessageBox.Show("A newer version is available." +
                                    "\n\nCurrent version:    " + currentVersion +
                                      "\nAvailable version:  " + newestVersion +
                                    "\n\nWould you like to install the newest version?\n" +
                                        "Clicking yes will start the download.",
                                        "Update available", MessageBoxButton.YesNo, MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            string link = "https://github.com/hudriwudi/youtube-dl-GUI/releases/download/" + newestVersion + "/yt-dl-GUI-setup.msi";
                            Process.Start("explorer.exe", link); // opens link in default browser
                        }
                    }
                }
                else
                {
                    // send report
                    string subject = "YouTube-dl GUI => Access To GitHub API Denied";
                    string textBody = "<pre>" +
                                      "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString() +
                                    "\nUser: " + Environment.UserName +
                                  "\n\nInformation about the newest release couldn't be accessed." +
                                  "\n\nResponse:\n" + data +
                                      "<pre>";

                    App.SendEmail(subject, textBody);
                }
            }


            // check wheter any crash reports have been stored and send them

            var tempFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.txt*");
            List<string> txtFiles = new List<string>();

            foreach (string file in tempFiles)
            {
                if (file.Contains("Crash report"))
                    txtFiles.Add(file);
            }

            if (txtFiles.Count != 0)
            {
                StreamReader reader;
                foreach (string file in txtFiles)
                {
                    reader = new StreamReader(file);
                    string text = reader.ReadToEnd();
                    reader.Close();

                    int startIndex = 9; // "Subject: "
                    int stopIndex = text.IndexOf("Body:") - 2;
                    string subject = text[startIndex..stopIndex];

                    startIndex = text.IndexOf("Body:") + 6; // "Body: "
                    stopIndex = text.Length;
                    string textBody = text[startIndex..stopIndex];

                    File.Delete(file);

                    App.SendEmail(subject, textBody);
                }
            }


            path = Directory.GetCurrentDirectory().ToString() + "\\downloaded songs";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            // delete excess, oldest 20 files
            if (Directory.Exists(path))
            {
                int count = Directory.GetFiles(path).Length;
                if (count > 20)
                {
                    List<FileInfo> files = new DirectoryInfo(path).GetFiles().OrderByDescending(f => f.LastWriteTime).ToList();
                    for (int i = 20; i < files.Count; i++)
                    {
                        files[i].Delete();
                    }
                }
            }

            path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + "\\youtube-dl";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem selectedTreeView = (TreeViewItem)sender;

            switch (selectedTreeView.Header)
            {
                case "Spotify":
                    this.Hide();
                    winSpotify = new Spotify(songList);
                    winSpotify.Owner = this;
                    if (!winSpotify.unableToConnect)
                        winSpotify.Show();
                    break;

                case "Song list":
                    winSongList = new SongList(songList);
                    winSongList.Owner = this;
                    winSongList.Show();
                    break;

                case "Downloaded songs":
                    winDownloads = new Downloaded();
                    winDownloads.Show();
                    break;
            }
        }

        public List<Song> YoutubeSearch(string searchtext, int maxResults)
        {
            List<Song> list = new();
            List<string> strIndividualVideos;

            string apiKey = DecryptText(ConfigurationManager.AppSettings["APIKey" + APICredentialsIncrementor]);

            searchtext = searchtext.Replace(" ", "%20");
            searchtext = searchtext.Replace("&", "%38");

            string link1 = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&maxResults=" + maxResults + "&q=";
            string link2 = "&fields=items(id%2FvideoId%2Csnippet(thumbnails%2Fdefault%2Furl%2Ctitle%2C%20channelTitle%2C%20channelId))&uploadType=video&key=";
            string fullLink = link1 + searchtext + link2 + apiKey;

            string webData = GetWebData(fullLink, null);

            if (quotaExceeded)
                return QuotaExceeded(searchtext, maxResults);

            if (webData.Contains("&amp;"))
                webData = System.Text.RegularExpressions.Regex.Replace(webData, "&amp;", "&");

            // seperate data into chunks containing individual songs
            bool abort = false;
            int startIndex, stopIndex;
            strIndividualVideos = new List<string>();
            do
            {
                stopIndex = webData.IndexOf('}', webData.IndexOf("channelTitle")) + 1;

                if (webData.IndexOf("channelTitle") != webData.LastIndexOf("channelTitle"))
                {
                    strIndividualVideos.Add(webData[0..stopIndex]);
                    webData = webData.Remove(0, stopIndex);
                }
                else
                {
                    abort = true;
                    strIndividualVideos.Add(webData);
                }
            }
            while (!abort);


            // sort out relevant info
            foreach (var item in strIndividualVideos)
            {
                try
                {
                    Song newSong = new();

                    startIndex = item.IndexOf("videoId") + 11;
                    stopIndex = item.IndexOf('}', startIndex) - 8;
                    newSong.ID = item[startIndex..stopIndex];
                    newSong.Link = "https://www.youtube.com/watch?v=" + newSong.ID;

                    startIndex = item.IndexOf("channelId") + 13;
                    stopIndex = item.IndexOf("title") - 12;
                    newSong.ChannelId = item[startIndex..stopIndex];

                    startIndex = item.IndexOf(@"""title""") + 10;
                    stopIndex = item.IndexOf("thumbnails") - 12;
                    newSong.Title = item[startIndex..stopIndex];

                    startIndex = item.IndexOf("channelTitle") + 16;
                    stopIndex = item.IndexOf('}', startIndex) - 8;
                    newSong.Channel = item[startIndex..stopIndex];

                    startIndex = item.IndexOf("url") + 7;
                    stopIndex = item.IndexOf('}', startIndex) - 12;
                    newSong.ThumbnailSource = item[startIndex..stopIndex];

                    // access API again to obtain duration time
                    string linkAPI = "https://youtube.googleapis.com/youtube/v3/videos?part=contentDetails%2C%20snippet%2C%20statistics&id=" + newSong.ID + "&fields=items(contentDetails%2Fduration%2C%20snippet%2Fdescription%2C%20statistics%2FviewCount)&key=" + apiKey;
                    webData = GetWebData(linkAPI, null);

                    if (quotaExceeded)
                        return QuotaExceeded(searchtext, maxResults);

                    startIndex = webData.IndexOf(@"""duration"":") + 13;
                    stopIndex = webData.IndexOf('}', startIndex) - 8;

                    string strTime = webData[startIndex..stopIndex];
                    TimeSpan time = XmlConvert.ToTimeSpan(strTime);
                    newSong.DurationMS = time.TotalMilliseconds;

                    newSong.Duration = MStoSongDuration(newSong.DurationMS);

                    startIndex = webData.IndexOf("description") + 15;
                    stopIndex = webData.IndexOf("contentDetails") - 18;
                    newSong.Description = webData[startIndex..stopIndex];

                    startIndex = webData.IndexOf("viewCount") + 13;
                    stopIndex = webData.IndexOf('}', startIndex) - 8;
                    newSong.ViewCount = Convert.ToDouble(webData[startIndex..stopIndex]);

                    /* the following snippet is currently commented out as it drastically slowed down performance time
                    
                    // find out whether it's an official artist channel
                    linkAPI = "https://yt.lemnoslife.com/channels?part=approval&id=" + newSong.ChannelId;
                    webData = GetWebData(linkAPI, null);
                    if (webData != null)
                    {
                        if (webData.Contains("Official Artist Channel"))
                            newSong.IsOfficialArtistChannel = true;
                    }
                    */

                    list.Add(newSong);
                }
                catch (System.ArgumentOutOfRangeException)
                { } // faulty video doesn't get added to the list
            }

            return list;
        }

        public string GetWebData(string link, AuthenticationHeaderValue header)
        {
            string webData;
            try
            {
                HttpClient client = new();
                var request = new HttpRequestMessage(HttpMethod.Get, link);
                if (header != null)
                    client.DefaultRequestHeaders.Add(header.Scheme, header.Parameter);
                var response = client.Send(request);
                var reader = new StreamReader(response.Content.ReadAsStream());
                webData = reader.ReadToEnd();
                webData = HttpUtility.HtmlDecode(webData);
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException)
                {
                    if (attemptCounter < 5)
                    {
                        attemptCounter++;
                        return GetWebData(link, null);
                    }
                    else // try with new API Key
                    {
                        attemptCounter = 0;
                        link = link.Remove(link.IndexOf("key=") + 4);
                        APICredentialsIncrementor++;
                        link += DecryptText(ConfigurationManager.AppSettings["APIKey" + APICredentialsIncrementor]);
                        return GetWebData(link, null);
                    }
                }
                else if (ex is InvalidOperationException) // crash report -> was thrown when accessing https://yt.lemnoslife.com/channels?part=approval&id=ChannelID, wasn't able to recreate the exception
                    return null;
                else
                    throw;
            }

            if (webData.Contains("quotaExceeded"))
                quotaExceeded = true;

            return webData;
        }

        public static string MStoSongDuration(double DurationMS)
        {
            // format time into displayable format
            int hours = 0;
            int minutes = 0;
            int seconds;
            string strSeconds;
            string duration;

            if (DurationMS > 59999) // > 1min
            {
                minutes = Convert.ToInt32(Math.Floor(DurationMS / 60000));
                seconds = Convert.ToInt32((DurationMS % 60000) / 1000);
            }
            else
                seconds = Convert.ToInt32(DurationMS / 1000);

            if (seconds < 10)
                strSeconds = "0" + seconds.ToString();
            else
                strSeconds = seconds.ToString();

            if (DurationMS > 3599999) // >1h
                hours = Convert.ToInt32(DurationMS / 3600000);

            if (hours == 0)
                duration = minutes + ":" + strSeconds;
            else
                duration = hours + ":" + minutes + ":" + strSeconds;

            return duration;
        }

        private void CmdSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox_LostFocus(searchBox, e); // workaround for searchBox.isFocused = true

            if (!IsConnectedToInternet())
            {
                MessageBox.Show("Could not connect to the Internet.\n\nPlease check whether you have a stable internet connection.", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!searchBox.Text.Contains('-'))
                return;

            // access youtube api and display content in listbox

            searchList.Clear();
            searchtext = searchBox.Text;
            searchList = YoutubeSearch(searchtext, 10);

            if (searchList != null) // no API limit reached
            {
                datagrid.ItemsSource = null;
                datagrid.ItemsSource = searchList;
            }

            cmdRecommend.IsEnabled = true;
        }

        public static bool IsConnectedToInternet()
        {
            try
            {
                Ping myPing = new Ping();
                String host = "google.com";
                byte[] buffer = new byte[32];
                int timeout = 1000;
                PingOptions pingOptions = new PingOptions();
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                return (reply.Status == IPStatus.Success);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public List<Song> QuotaExceeded(string searchtext, int maxResults)
        {
            if (APICredentialsIncrementor < 30)
            {
                APICredentialsIncrementor++;
                quotaExceeded = false;
                return YoutubeSearch(searchtext, maxResults); // restart the search using a new API Key
            }
            else
            {
                MessageBox.Show("Unfortunately the daily limit of 3000 requests to the Youtube Data API has been reached. Please try it again tomorrow. If the problem keeps coming up, please don't hesitate contacting the developer.", "Error: Limit Reached", MessageBoxButton.OK, MessageBoxImage.Error);
                List<Song> LimitReached = null;
                return LimitReached;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!searchBox.Text.Contains("audio"))
                searchBox.Text += " audio";

            if (!searchBox.Text.Contains('-'))
            {
                MessageBox.Show(@"Please use the format ""artist - song"" or enter the information in the fields above.", "Wrong Format", MessageBoxButton.OK, MessageBoxImage.Information);
                searchBox.SelectAll();
            }
            else
            {
                int stopIndex = searchBox.Text.LastIndexOf('-') - 1;
                artist = searchBox.Text[0..stopIndex];

                int startIndex = stopIndex + 3;
                stopIndex = searchBox.Text.IndexOf(" audio");
                songname = searchBox.Text[startIndex..stopIndex];

                txtArtist.Text = artist;
                txtSongname.Text = songname;
                txtArtist.UpdateLayout();
                txtSongname.UpdateLayout();
            }
        }

        private void CmdGridAdd_Click(object sender, RoutedEventArgs e)
        {
            AddToSongList();
        }

        private void CmdRecommend_Click(object sender, RoutedEventArgs e)
        {
            int index = Spotify.RankYoutubeSearch(searchList, 0, false);
            datagrid.SelectedIndex = index;
            AddToSongList();
        }

        private void AddToSongList()
        {
            Song song = (Song)datagrid.SelectedItem;
            song.Artist = txtArtist.Text;
            song.Songname = txtSongname.Text;
            songList.Add(song);

            if (winSongList != null)
                if (winSongList.IsVisible)
                    winSongList.Close();

            winSongList = new(songList);
            winSongList.Owner = this;
            winSongList.Show();
        }

        private void TxtArtist_TextChanged(object sender, TextChangedEventArgs e)
        {
            artist = txtArtist.Text;
            UpdateSearchBar();
        }

        private void TxtSongname_TextChanged(object sender, TextChangedEventArgs e)
        {
            songname = txtSongname.Text;
            UpdateSearchBar();
        }

        private void UpdateSearchBar()
        {
            searchtext = artist + " - " + songname + " audio";
            searchBox.Text = searchtext;
            searchBox.UpdateLayout();
        }

        private void CmdThumbnail_MouseDoubleClick(object sender, System.Windows.Input.MouseEventArgs e)
        {
            PlayYoutubeVideo();
        }

        private void CmdPlay_Click(object sender, RoutedEventArgs e)
        {
            PlayYoutubeVideo();
        }

        private void PlayYoutubeVideo()
        {
            string link;
            int index = datagrid.SelectedIndex;
            Song chosenSong = (Song)datagrid.Items[index];
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

        private void TxtArtist_GotFocus(object sender, RoutedEventArgs e)
        {
            txtArtist.SelectAll();
        }

        private void TxtSongname_GotFocus(object sender, RoutedEventArgs e)
        {
            txtSongname.SelectAll();
        }

        public static string DecryptText(string input)
        {
            // Get the bytes of the string
            byte[] bytesToBeDecrypted = Convert.FromBase64String(input);
            byte[] passwordBytes = Encoding.UTF8.GetBytes("5&:MO6.rwr<%=#");
            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);

            byte[] bytesDecrypted = AES_Decrypt(bytesToBeDecrypted, passwordBytes);

            string result = Encoding.UTF8.GetString(bytesDecrypted);

            return result;
        }

        public static byte[] AES_Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes)
        {
            byte[] decryptedBytes = null;

            // Set your salt here, change it to meet your flavor:
            // The salt bytes must be at least 8 bytes.
            byte[] saltBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            using (MemoryStream ms = new MemoryStream())
            {
                using (Aes AES = Aes.Create("AesManaged"))
                {
                    AES.KeySize = 256;
                    AES.BlockSize = 128;

                    var key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, 1000);
                    AES.Key = key.GetBytes(AES.KeySize / 8);
                    AES.IV = key.GetBytes(AES.BlockSize / 8);

                    AES.Mode = CipherMode.CBC;

                    using (var cs = new CryptoStream(ms, AES.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);
                        cs.Close();
                    }
                    decryptedBytes = ms.ToArray();
                }
            }

            return decryptedBytes;
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
