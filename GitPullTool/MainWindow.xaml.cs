using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace GitPullTool
{
  /// <summary>
  /// Logique d'interaction pour MainWindow.xaml
  /// </summary>
  public partial class MainWindow: Window
  {
    private const string SettingsFile = "windowSettings.json";

    public ObservableCollection<RepoResult> Results { get; set; }
    

    public MainWindow()
    {
      InitializeComponent();
      Results = new ObservableCollection<RepoResult>();
      ResultsGrid.ItemsSource = Results;
      LoadWindowSettings();
      Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      SaveWindowSettings();
    }

    private void SaveWindowSettings()
    {
      try
      {
        var settings = new WindowSettings
        {
          Width = Width,
          Height = Height,
          Left = Left,
          Top = Top
        };

        var serializer = new XmlSerializer(typeof(WindowSettings));
        using (var writer = new StreamWriter(SettingsFile))
        {
          serializer.Serialize(writer, settings);
        }
      }
      catch { }
    }


    private void LoadWindowSettings()
    {
      try
      {
        if (!File.Exists(SettingsFile))
        {
          try
          {
            File.Create(SettingsFile);
          }
          catch (Exception)
          {
            return;
          }
        }

        var serializer = new XmlSerializer(typeof(WindowSettings));
        using (var reader = new StreamReader(SettingsFile))
        {
          var settings = (WindowSettings)serializer.Deserialize(reader);
          Width = settings.Width;
          Height = settings.Height;
          Left = settings.Left;
          Top = settings.Top;
        }
      }
      catch { }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new System.Windows.Forms.FolderBrowserDialog();
      if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        SourcePath.Text = dialog.SelectedPath;
      }
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
      Results.Clear();
      var path = SourcePath.Text;


      if (!Directory.Exists(path))
      {
        MessageBox.Show("Invalid path");
        return;
      }


      var repos = Directory.GetDirectories(path)
      .Where(d => Directory.Exists(Path.Combine(d, ".git")))
      .ToList();


      int total = repos.Count;
      int processed = 0;


      await Task.WhenAll(repos.Select(repo => Task.Run(() => ProcessRepo(repo, total, () => processed++))));
    }

    private void ProcessRepo(string repo, int total, Action increment)
    {
      var result = new RepoResult();
      result.Repo = repo;


      try
      {
        var status = RunGitCommand("status -sb", repo);
        result.Branch = status.Split('\n').FirstOrDefault();


        if (status.Contains(" M ") || status.Contains("??"))
        {
          result.Status = "SKIPPED";
          result.Message = "Local changes";
        }
        else
        {
          RunGitCommand("fetch --all", repo);
          RunGitCommand("pull --rebase --autostash", repo);
          result.Status = "OK";
        }
      }
      catch (Exception ex)
      {
        result.Status = "ERROR";
        result.Message = ex.Message;
      }


      Dispatcher.Invoke(() =>
      {
        Results.Add(result);
        increment();
        ProgressBar.Value = (double)Results.Count / total * 100;
      });
    }

    private string RunGitCommand(string args, string workingDir)
    {
      var psi = new ProcessStartInfo
      {
        FileName = "git",
        Arguments = args,
        WorkingDirectory = workingDir,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };


      using (var process = Process.Start(psi))
      {
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
          throw new Exception(process.StandardError.ReadToEnd());
        
        return output;
      }
    }
  }
}
