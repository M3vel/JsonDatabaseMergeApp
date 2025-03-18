using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Xml;
using JsonDatabaseMergeApp.Database;
using Microsoft.Win32;

namespace JsonDatabaseMergeApp
{
    public partial class MainWindow : Window
    {
        private string firstDatabasePath;
        private string secondDatabasePath;
        private bool isSidebarVisible = false;//1
        private List<Dictionary<string, object>> jsonData = new();//2

        public MainWindow()
        {
            InitializeComponent();
        }
        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)//1
        {
            double targetMargin = isSidebarVisible ? -250 : 0;
            ThicknessAnimation sidebarAnimation = new ThicknessAnimation
            {
                From = SidebarPanel.Margin,
                To = new Thickness(0, 0, targetMargin, 0),
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseInOut }
            };

            SidebarPanel.BeginAnimation(MarginProperty, sidebarAnimation);

            if (!isSidebarVisible)
            {
                SidebarPanel.Visibility = Visibility.Visible;
            }
            else
            {
                sidebarAnimation.Completed += (s, ev) => SidebarPanel.Visibility = Visibility.Hidden;
            }

            ToggleSidebarButton.Content = isSidebarVisible ? "❯" : "❮";
            isSidebarVisible = !isSidebarVisible;
        }

        private void BtnSelectFirst_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (openFileDialog.ShowDialog() == true)
            {
                firstDatabasePath = openFileDialog.FileName;
                TxtFirstDbPath.Text = firstDatabasePath;
                UpdateDatabaseSummary();//1
            }
        }

        private void BtnSelectSecond_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (openFileDialog.ShowDialog() == true)
            {
                secondDatabasePath = openFileDialog.FileName;
                TxtSecondDbPath.Text = secondDatabasePath;
                UpdateDatabaseSummary();//1
            }
        }

        private async void BtnCreateDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(firstDatabasePath) || string.IsNullOrEmpty(secondDatabasePath))
            {
                MessageBox.Show("Выберите обе базы данных перед объединением!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Сохранить объединённую базу",
                FileName = "MergedDatabase.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string savePath = saveFileDialog.FileName;
                await MergeDatabasesAsync(savePath);
            }
        }

        private async Task MergeDatabasesAsync(string savePath)
        {
            try
            {
                var progress = new Progress<int>(value => ProgressMerge.Value = value);
                await MergeWithProgressAsync(savePath, progress);
                MessageBox.Show("Объединённая база успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateDatabaseSummary();//1
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task MergeWithProgressAsync(string savePath, IProgress<int> progress)
        {
            progress.Report(0);
            List<Sample> firstDatabase = await Task.Run(() => ReadDatabase(firstDatabasePath));
            progress.Report(40);
            List<Sample> secondDatabase = await Task.Run(() => ReadDatabase(secondDatabasePath));
            progress.Report(80);

            List<Sample> combinedSamples = MergeDatabases(firstDatabase, secondDatabase);
            progress.Report(90);

            if (combinedSamples.Count == 0)
            {
                MessageBox.Show("Объединение не выполнено: нет данных для сохранения!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(savePath, JsonSerializer.Serialize(new { samples = combinedSamples }, jsonOptions));
            progress.Report(100);
        }

        private List<Sample> ReadDatabase(string filePath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                var jsonDoc = JsonSerializer.Deserialize<JsonDocument>(jsonContent);
                var samples = new List<Sample>();

                if (!jsonDoc.RootElement.TryGetProperty("samples", out var samplesArray) || samplesArray.ValueKind != JsonValueKind.Array)
                {
                    MessageBox.Show($"Ошибка: В файле {filePath} отсутствует корректный массив 'samples'.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return samples;
                }

                foreach (var sampleElement in samplesArray.EnumerateArray())
                {
                    var sample = new Sample
                    {
                        Id = sampleElement.GetProperty("id").GetInt32(),
                        SurveyId = sampleElement.GetProperty("survey_id").GetInt32(),
                        Name = sampleElement.GetProperty("name").GetString(),
                        ScanTime = sampleElement.GetProperty("scan_time").GetString(),
                        DateAdded = sampleElement.GetProperty("date_added").GetString(),
                        Status = sampleElement.GetProperty("status").GetInt32(),
                        Remarks = sampleElement.TryGetProperty("remarks", out var remarks) ? remarks.GetString() : "",
                        Concentrations = new Dictionary<string, double>()
                    };

                    if (sampleElement.TryGetProperty("concentrations", out var concentrations))
                    {
                        foreach (var concentrationElement in concentrations.EnumerateObject())
                        {
                            sample.Concentrations[concentrationElement.Name] = concentrationElement.Value.GetDouble();
                        }
                    }

                    samples.Add(sample);
                }

                return samples;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения базы данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<Sample>();
            }
        }

        private List<Sample> MergeDatabases(List<Sample> firstDatabase, List<Sample> secondDatabase)
        {
            /*var mergedSamples = new HashSet<(int Id, int SurveyId)>();
            var result = new List<Sample>();

            foreach (var sample in firstDatabase.Concat(secondDatabase))
            {
                var key = (sample.Id, sample.SurveyId);
                if (!mergedSamples.Contains(key))
                {
                    mergedSamples.Add(key);
                    result.Add(sample);
                }
            }

            return result;*/

            var mergedSamples = new Dictionary<(int Id, int SurveyId), Sample>();

            foreach (var sample in firstDatabase)
            {
                var key = (sample.Id, sample.SurveyId);
                if (!mergedSamples.ContainsKey(key))
                {
                    mergedSamples[key] = sample;
                }
            }

            foreach (var sample in secondDatabase)
            {
                var key = (sample.Id, sample.SurveyId);
                if (!mergedSamples.ContainsKey(key))
                {
                    mergedSamples[key] = sample;
                }
            }

            return mergedSamples.Values.ToList();
        }

        private void UpdateDatabaseSummary()//1
        {
            DatabaseTreeView.Items.Clear();

            if (string.IsNullOrEmpty(firstDatabasePath) && string.IsNullOrEmpty(secondDatabasePath))
                return;

            var databases = new Dictionary<string, List<Sample>>();

            if (!string.IsNullOrEmpty(firstDatabasePath))
                databases[firstDatabasePath] = ReadDatabase(firstDatabasePath);
            if (!string.IsNullOrEmpty(secondDatabasePath))
                databases[secondDatabasePath] = ReadDatabase(secondDatabasePath);

            foreach (var dbEntry in databases)
            {
                string dbName = System.IO.Path.GetFileName(dbEntry.Key);
                var databaseNode = new TreeViewItem { Header = dbName, Tag = dbEntry.Key };

                var surveys = dbEntry.Value.GroupBy(s => (s.SurveyId, s.Name));

                foreach (var survey in surveys)
                {
                    var surveyNode = new TreeViewItem { Header = $"Съёмка {survey.Key.SurveyId} - {survey.Key.Name}" };

                    foreach (var sample in survey)
                    {
                        var sampleNode = new TreeViewItem { Header = $"Запись {sample.Id}" };

                        var scanTimeNode = new TreeViewItem { Header = $"Время сканирования: {sample.ScanTime}" };
                        var dateAddedNode = new TreeViewItem { Header = $"Дата добавления: {sample.DateAdded}" };

                        sampleNode.Items.Add(scanTimeNode);
                        sampleNode.Items.Add(dateAddedNode);

                        surveyNode.Items.Add(sampleNode);
                    }

                    databaseNode.Items.Add(surveyNode);
                }

                DatabaseTreeView.Items.Add(databaseNode);
            }
        }
        private void BtnSelectDb_Click(object sender, RoutedEventArgs e)//2- доделать
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtDbPath.Text = openFileDialog.FileName;
                LoadSurveyIds(openFileDialog.FileName);
                UpdateDatabaseSummary();
            }
        }

        private void LoadSurveyIds(string filePath)//2
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

                if (!jsonElement.TryGetProperty("samples", out JsonElement samplesElement) ||
                    samplesElement.ValueKind != JsonValueKind.Array)
                {
                    MessageBox.Show("Ошибка: JSON должен содержать ключ 'samples' с массивом объектов.");
                    return;
                }

                jsonData = new List<Dictionary<string, object>>();

                foreach (var element in samplesElement.EnumerateArray())
                {
                    var dictionary = new Dictionary<string, object>();

                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Object)
                        {
                            // Если значение - объект, десериализуем его в Dictionary
                            dictionary[property.Name] = JsonSerializer.Deserialize<Dictionary<string, object>>(property.Value.GetRawText());
                        }
                        else
                        {
                            dictionary[property.Name] = property.Value.ValueKind switch
                            {
                                JsonValueKind.String => property.Value.GetString(),
                                JsonValueKind.Number => property.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null,
                                _ => property.Value.ToString()
                            };
                        }
                    }

                    jsonData.Add(dictionary);
                }

                var surveyIds = jsonData
                    .Where(x => x.ContainsKey("survey_id"))
                    .Select(x => x["survey_id"]?.ToString())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .ToList();

                CmbSurveyId.ItemsSource = surveyIds;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки ID съемок: {ex.Message}");
            }
        }

        private void BtnCreateFilteredDatabase_Click(object sender, RoutedEventArgs e)//2
        {
            if (string.IsNullOrEmpty(TxtDbPath.Text) || CmbSurveyId.SelectedItem == null)
            {
                MessageBox.Show("Выберите базу данных и ID съемки.");
                return;
            }

            string selectedId = CmbSurveyId.SelectedItem.ToString();
            var filteredData = jsonData
                .Where(x => x.ContainsKey("survey_id") && x["survey_id"].ToString() == selectedId)
                .ToList();

            if (!filteredData.Any())
            {
                MessageBox.Show("Нет данных для выбранного ID съемки.");
                return;
            }

            string newFilePath = Path.Combine(Path.GetDirectoryName(TxtDbPath.Text), $"Filtered_{selectedId}.json");

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

            // Оборачиваем отфильтрованные данные обратно в объект { "samples": [...] }
            var resultJson = new Dictionary<string, object> { { "samples", filteredData } };

            File.WriteAllText(newFilePath, JsonSerializer.Serialize(resultJson, jsonOptions));

            MessageBox.Show($"Новая база создана: {newFilePath}");
        }
    }
}
