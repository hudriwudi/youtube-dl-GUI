using System.Windows;

namespace youtube_dl_v2
{
    /// <summary>
    /// Interaktionslogik für Downloaded_ChooseSong.xaml
    /// </summary>
    public partial class Downloaded_ChooseSong : Window
    {
        string[] filePaths;
        public int index;

        public Downloaded_ChooseSong(string[] filePaths)
        {
            InitializeComponent();
            this.filePaths = filePaths;
            cmdChoose.IsDefault = true;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            listBox.ItemsSource = filePaths;
        }

        private void CmdChoose_Click(object sender, RoutedEventArgs e)
        {
            if (listBox.SelectedIndex == -1)
                MessageBox.Show("Please select one of the items.", "No item selected");
            else
            {
                index = listBox.SelectedIndex;
                this.Hide();
            }
        }
    }
}
