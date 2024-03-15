using Microsoft.Win32;
using System.IO;
using System.Windows;
using MimeKit;
using MailKit.Net.Imap;
using MailKit;
namespace SearchWord_WpfApp
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cancellationTokenSource;
        private SaveFileDialog dialog;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void browseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                   directoryTextBox.Text = dialog.FolderName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}");
                }
            }

        }

        private async Task SearchWordInFilesAsync(string directoryPath, string searchWord, CancellationToken cancellationToken, IProgress<int> progress)
        {
            string[] files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
            int totalFiles = files.Length;
            int processedFiles = 0;

            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    MessageBox.Show("Search operation cancelled.");
                    return;
                }

                try
                {
                    string content = await File.ReadAllTextAsync(file);
                    if (content.Contains(searchWord))
                    {
                        foundWordPathListBox.Items.Add(file);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while processing file {file}: {ex.Message}");
                }

                processedFiles++;
                int percentComplete = (int)((double)processedFiles / totalFiles * 100);
                progress?.Report(percentComplete);
                percentLabel.Content = $"{percentComplete} %";
            }
        }


        private void resetButton_Click(object sender, RoutedEventArgs e)
        {
            ClearParameters();
        }
        private void ClearParameters()
        {
            directoryTextBox.Text = string.Empty;
            searchWordTextBox.Text = string.Empty;
            foundWordPathListBox.Items.Clear();
            cancellationTokenSource?.Cancel();
            sendEmailCheckBox.IsChecked = false;
            saveToFileCheckBox.IsChecked = false;
            saveToFileButton.IsEnabled = false;
            progressBar.Value = 0;
            percentLabel.Content = "0 %";
            foundWordPathListBox.Items.Clear();
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
            progressBar.Value = 0;
            percentLabel.Content = "0 %";
        }
        private async Task PerformSearchAsync()
        {
            string directoryPath = directoryTextBox.Text;
            string searchWord = searchWordTextBox.Text;

            if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(searchWord))
            {
                MessageBox.Show("Please enter directory path and search word.");
                return;
            }

            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                var progress = new Progress<int>(percentComplete => progressBar.Value = percentComplete);
                await SearchWordInFilesAsync(directoryPath, searchWord, cancellationTokenSource.Token, progress);

                if (progressBar.Value == 100)
                {
                    if (sendEmailCheckBox.IsChecked == true)
                    {
                        await AddMassagEmailsAsync();
                        SaveToFile("ResultSearch.txt");
                    }

                    if (saveToFileCheckBox.IsChecked == true && dialog != null && !string.IsNullOrEmpty(dialog.FileName))
                    {
                        SaveToFile(dialog.FileName);
                    }
                    else if (saveToFileCheckBox.IsChecked == true)
                    {
                        SaveToFile("ResultSearch.txt");
                    }

                    if (MessageBox.Show("The search operation has been completed.", "Search Completed", MessageBoxButton.OK) == MessageBoxResult.OK)
                    {
                        // ClearParameters();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Search operation cancelled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private async void startButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearchAsync();
        }
        private  void saveToFileButton_Click(object sender, RoutedEventArgs e)
        {
            dialog = new SaveFileDialog();
            dialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            dialog.ShowDialog();
        }
        private async void SaveToFile(string patch)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(patch))
                {
                    foreach (var item in foundWordPathListBox.Items)
                    {
                        await writer.WriteLineAsync(item.ToString());
                    }
                }
                MessageBox.Show("Results saved to file successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving results to file: {ex.Message}");
            }

        }

        private void saveToFileCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            saveToFileButton.IsEnabled = true;
        }
        private void saveToFileCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            saveToFileButton.IsEnabled = false;
        }
        private async Task AddMassagEmailsAsync()
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("SearchWord", "cyberdron@ukr.net"));
                message.To.Add(new MailboxAddress("Andrii", "cyberdron@ukr.net"));
                message.Subject = "Результати пошуку";

                var bodyBuilder = new BodyBuilder();
                foreach (var item in foundWordPathListBox.Items)
                {
                    bodyBuilder.TextBody += item.ToString() + Environment.NewLine;
                    var attachment = new MimePart("application", "octet-stream")
                    {
                        Content = new MimeContent(File.OpenRead(item.ToString()), ContentEncoding.Default),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = Path.GetFileName(item.ToString())
                    };
                    bodyBuilder.Attachments.Add(attachment);
                }
                message.Body = bodyBuilder.ToMessageBody();

                await AddMessageToFolderAsync(message, "INBOX");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while creating message: {ex.Message}");
            }
        }

        private async Task AddMessageToFolderAsync(MimeMessage message, string folderName)
        {
            string host = "imap.ukr.net";
            int port = 993; 
            string username = "cyberdron@ukr.net";
            string password = "wBizME97BKBw8OkU";

            try
            {
                using (var client = new ImapClient())
                {
                    await client.ConnectAsync(host, port, true);

                    await client.AuthenticateAsync(username, password);

                    var folder = await client.GetFolderAsync(folderName);

                    await folder.AppendAsync(message);

                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while adding message to folder: {ex.Message}");
            }
        }
    }
}
