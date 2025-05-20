using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        private string thirdDatabasePath;
        private bool isSidebarVisible = false;//1
        private List<Dictionary<string, object>> jsonData = new();//2
        private string integrationReportsDir;
        private string exportReportsDir;

        public MainWindow()
        {
            InitializeComponent();
            InitializeReportPaths();
            EnsureReportDirectoriesExist();
        }

        private void InitializeReportPaths()
        {
            //string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string userDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            integrationReportsDir = Path.Combine(userDocuments, "Reports", "IntegrationReports");
            exportReportsDir = Path.Combine(userDocuments, "Reports", "ExportReports");
        }

        private void EnsureReportDirectoriesExist()
        {
            Directory.CreateDirectory(integrationReportsDir);
            Directory.CreateDirectory(exportReportsDir);
        }

        private void BtnOpenIntegrationReports_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(integrationReportsDir))
                    Process.Start("explorer.exe", integrationReportsDir);
                else
                    MessageBox.Show("Папка с отчётами ещё не создана.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии папки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenExportReports_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(exportReportsDir))
                    Process.Start("explorer.exe", exportReportsDir);
                else
                    MessageBox.Show("Папка с отчётами ещё не создана.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии папки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                string selectedPath = Path.GetFullPath(openFileDialog.FileName);

                if (!string.IsNullOrEmpty(secondDatabasePath) &&
                    string.Equals(Path.GetFullPath(secondDatabasePath), selectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Вы не можете выбрать одну и ту же базу данных дважды.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                firstDatabasePath = selectedPath;
                TxtFirstDbPath.Text = firstDatabasePath;
                UpdateDatabaseSummary();
            }
        }

        private void BtnSelectSecond_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = Path.GetFullPath(openFileDialog.FileName);

                if (!string.IsNullOrEmpty(firstDatabasePath) &&
                    string.Equals(Path.GetFullPath(firstDatabasePath), selectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Вы не можете выбрать одну и ту же базу данных дважды.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                secondDatabasePath = selectedPath;
                TxtSecondDbPath.Text = secondDatabasePath;
                UpdateDatabaseSummary();
            }
        }

        private async void BtnCreateDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(firstDatabasePath) || string.IsNullOrEmpty(secondDatabasePath))
            {
                MessageBox.Show("Выберите обе базы данных перед объединением!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string validationPath1 = TxtFirstDbPath.Text;
            string validationPath2 = TxtSecondDbPath.Text;

            if (!ValidateJsonDatabase(validationPath1, out string validationError1))
            {
                DateTime startTime = DateTime.Now;
                var ex = new Exception(validationError1);
                GenerateOperationErrorReport("Интеграция", validationPath1, null, ex, startTime, integrationReportsDir);

                MessageBox.Show($"Ошибка коррекности первой базы данных:\n{validationError1}", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ValidateJsonDatabase(validationPath2, out string validationError2))
            {
                DateTime startTime = DateTime.Now;
                var ex = new Exception(validationError2);
                GenerateOperationErrorReport("Интеграция", validationPath2, null, ex, startTime, integrationReportsDir);

                MessageBox.Show($"Ошибка коррекности второй базы данных:\n{validationError2}", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ProgressMerge.Value = 0;
            await MergeDatabasesAsync();
        }

        private async Task MergeDatabasesAsync()
        {
            try
            {
                var progress = new Progress<int>(value => ProgressMerge.Value = value);
                List<Sample> combinedSamples = await MergeWithProgressAsync(progress);

                if (combinedSamples == null || combinedSamples.Count == 0)
                {
                    MessageBox.Show("Объединение не выполнено: нет данных для сохранения!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(savePath, JsonSerializer.Serialize(new { samples = combinedSamples }, jsonOptions));
                    GenerateMergeReport(savePath, combinedSamples);
                    MessageBox.Show("Объединённая база успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ProgressMerge.Value = 0;
                UpdateDatabaseSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<List<Sample>> MergeWithProgressAsync(IProgress<int> progress)
        {
            progress.Report(0);
            List<Sample> firstDatabase = await Task.Run(() => ReadDatabase(firstDatabasePath));
            progress.Report(40);
            List<Sample> secondDatabase = await Task.Run(() => ReadDatabase(secondDatabasePath));
            progress.Report(80);

            List<Sample> combinedSamples = MergeDatabases(firstDatabase, secondDatabase);
            progress.Report(100);

            return combinedSamples;
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
                        Remarks = sampleElement.TryGetProperty("Remarks", out var remarks) ? remarks.GetString() : "",
                        MotherLiquorSerialNumber = sampleElement.TryGetProperty("Mother liquor serial number", out var liquorNumber) ? liquorNumber.GetString() : null,
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
            var mergedSamples = new Dictionary<(int Id, int SurveyId), Sample>();

            foreach (var sample in firstDatabase)
            {
                var key = (sample.Id, sample.SurveyId);
                mergedSamples[key] = sample;
            }

            foreach (var sample in secondDatabase)
            {
                var key = (sample.Id, sample.SurveyId);

                if (!mergedSamples.ContainsKey(key))
                {
                    mergedSamples[key] = sample;
                }
                else
                {
                    var existing = mergedSamples[key];

                    if (string.IsNullOrWhiteSpace(existing.Remarks) && !string.IsNullOrWhiteSpace(sample.Remarks))
                        existing.Remarks = sample.Remarks;

                    if (string.IsNullOrWhiteSpace(existing.MotherLiquorSerialNumber) && !string.IsNullOrWhiteSpace(sample.MotherLiquorSerialNumber))
                        existing.MotherLiquorSerialNumber = sample.MotherLiquorSerialNumber;

                }
            }

            return mergedSamples.Values.ToList();
        }

        private void UpdateDatabaseSummary()
        {
            DatabaseTreeView.Items.Clear();

            if (string.IsNullOrEmpty(firstDatabasePath) && string.IsNullOrEmpty(secondDatabasePath) && string.IsNullOrEmpty(thirdDatabasePath))
                return;

            var databases = new Dictionary<string, List<Sample>>();

            if (!string.IsNullOrEmpty(firstDatabasePath))
                databases[firstDatabasePath] = ReadDatabase(firstDatabasePath);
            if (!string.IsNullOrEmpty(secondDatabasePath))
                databases[secondDatabasePath] = ReadDatabase(secondDatabasePath);
            if (!string.IsNullOrEmpty(thirdDatabasePath))
                databases[thirdDatabasePath] = ReadDatabase(thirdDatabasePath);

            foreach (var dbEntry in databases)
            {
                string dbName = System.IO.Path.GetFileName(dbEntry.Key);
                var databaseNode = new TreeViewItem { Header = dbName, Tag = dbEntry.Key };

                var surveys = dbEntry.Value.GroupBy(s => s.SurveyId);

                foreach (var survey in surveys)
                {
                    var surveyNode = new TreeViewItem { Header = $"Съёмка {survey.Key}" };

                    foreach (var sample in survey)
                    {
                        var sampleNode = new TreeViewItem { Header = $"Проба {sample.Id}" };

                        var nameNode = new TreeViewItem { Header = $"Название пробы: {sample.Name}" };
                        var scanTimeNode = new TreeViewItem { Header = $"Время сканирования: {sample.ScanTime}" };
                        var dateAddedNode = new TreeViewItem { Header = $"Дата добавления: {sample.DateAdded}" };

                        sampleNode.Items.Add(nameNode);
                        sampleNode.Items.Add(scanTimeNode);
                        sampleNode.Items.Add(dateAddedNode);

                        surveyNode.Items.Add(sampleNode);
                    }

                    databaseNode.Items.Add(surveyNode);
                }

                DatabaseTreeView.Items.Add(databaseNode);
            }
        }
        private void BtnSelectDb_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;

                thirdDatabasePath = selectedPath;
                TxtDbPath.Text = thirdDatabasePath;

                LoadSurveyIds(selectedPath);
                UpdateDatabaseSummary();
            }
        }

        private void LoadSurveyIds(string filePath)//2
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

                if (!jsonElement.TryGetProperty("samples", out JsonElement samplesElement) || samplesElement.ValueKind != JsonValueKind.Array)
                {
                    MessageBox.Show("Ошибка: JSON должен содержать ключ 'samples' с массивом записей.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var surveyIds = new HashSet<int>();

                foreach (var sample in samplesElement.EnumerateArray())
                {
                    int surveyId = sample.GetProperty("survey_id").GetInt32();
                    surveyIds.Add(surveyId);
                }

                CmbSurveyId.ItemsSource = surveyIds;
                CmbSurveyId.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки ID съемки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnCreateFilteredDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtDbPath.Text))
            {
                MessageBox.Show("Выберите базу данных перед экспортом!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string validationPath = TxtDbPath.Text;

            if (!ValidateJsonDatabase(validationPath, out string validationError))
            {
                DateTime startTime = DateTime.Now;
                var ex = new Exception(validationError);
                GenerateOperationErrorReport("Экспорт", validationPath, null, ex, startTime, exportReportsDir);

                MessageBox.Show($"Ошибка валидации базы данных:\n{validationError}", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int selectedSurveyId = (int)CmbSurveyId.SelectedItem;

            ProgressExport.Value = 0;
            await ExportFilteredDatabaseAsync(selectedSurveyId); // перенос логики
        }

        private async Task ExportFilteredDatabaseAsync(int selectedSurveyId)
        {
            try
            {
                var progress = new Progress<int>(value => ProgressExport.Value = value);

                List<Sample> filteredSamples = await FilterDatabaseWithProgressAsync(selectedSurveyId, progress);

                if (filteredSamples == null || filteredSamples.Count == 0)
                {
                    MessageBox.Show("Нет данных для экспорта по выбранному ID съёмки.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    Title = "Экспортировать данные",
                    FileName = "FilteredDatabase.json"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string savePath = saveFileDialog.FileName;
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(savePath, JsonSerializer.Serialize(new { samples = filteredSamples }, jsonOptions));
                    GenerateExportReport(savePath, filteredSamples, selectedSurveyId);
                    MessageBox.Show("Данные успешно экспортированы!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ProgressExport.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при экспорте данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<List<Sample>> FilterDatabaseWithProgressAsync(int selectedSurveyId, IProgress<int> progress)
        {
            progress.Report(0);

            string dbPath = TxtDbPath.Text;
            List<Sample> database = await Task.Run(() => ReadDatabase(dbPath));

            progress.Report(50);

            List<Sample> filteredSamples = database.Where(s => s.SurveyId == selectedSurveyId).ToList();

            progress.Report(100);

            return filteredSamples;
        }
        private bool ValidateJsonDatabase(string filePath, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                string jsonContent = File.ReadAllText(filePath);

                using JsonDocument document = JsonDocument.Parse(jsonContent);
                if (!document.RootElement.TryGetProperty("samples", out JsonElement samples) || samples.ValueKind != JsonValueKind.Array)
                {
                    errorMessage = "Файл должен содержать корневой элемент 'samples' в виде массива.";
                    return false;
                }

                var keySet = new HashSet<(int id, int surveyId)>();

                foreach (JsonElement sample in samples.EnumerateArray())
                {
                    // ID
                    if (!sample.TryGetProperty("id", out JsonElement idElem) || idElem.ValueKind != JsonValueKind.Number || !idElem.TryGetInt32(out int id) || id <= 0)
                    {
                        errorMessage = "Один из образцов содержит некорректное поле 'id' (должно быть числом > 0).";
                        return false;
                    }

                    // Survey ID
                    if (!sample.TryGetProperty("survey_id", out JsonElement surveyIdElem) || surveyIdElem.ValueKind != JsonValueKind.Number || !surveyIdElem.TryGetInt32(out int surveyId) || surveyId <= 0)
                    {
                        errorMessage = "Один из образцов содержит некорректное поле 'survey_id' (должно быть числом > 0).";
                        return false;
                    }

                    // Уникальность (id, survey_id)
                    if (!keySet.Add((id, surveyId)))
                    {
                        errorMessage = $"Обнаружено дублирование записи с id = {id} и survey_id = {surveyId}.";
                        return false;
                    }

                    // scan_time
                    if (!sample.TryGetProperty("scan_time", out JsonElement scanTimeElem) || scanTimeElem.ValueKind != JsonValueKind.String || !DateTime.TryParse(scanTimeElem.GetString(), out _))
                    {
                        errorMessage = "Один из образцов содержит некорректное поле 'scan_time'.";
                        return false;
                    }

                    // date_added
                    if (!sample.TryGetProperty("date_added", out JsonElement dateAddedElem) || dateAddedElem.ValueKind != JsonValueKind.String || !DateTime.TryParse(dateAddedElem.GetString(), out _))
                    {
                        errorMessage = "Один из образцов содержит некорректное поле 'date_added'.";
                        return false;
                    }

                    // status
                    if (!sample.TryGetProperty("status", out JsonElement statusElem) || statusElem.ValueKind != JsonValueKind.Number || !statusElem.TryGetInt32(out int status) || status < 0)
                    {
                        errorMessage = "Один из образцов содержит некорректное поле 'status' (должно быть числом >= 0).";
                        return false;
                    }

                    if (!sample.TryGetProperty("Remarks", out JsonElement remarksElem) || remarksElem.ValueKind != JsonValueKind.String)
                    {
                        errorMessage = "Один из образцов содержит некорректное или отсутствующее поле 'Remarks'.";
                        return false;
                    }

                    // mother_liquor_serial_number
                    if (!sample.TryGetProperty("Mother liquor serial number", out JsonElement mlSerialElem) || mlSerialElem.ValueKind != JsonValueKind.String)
                    {
                        errorMessage = "Один из образцов содержит некорректное или отсутствующее поле 'Mother liquor serial number'.";
                        return false;
                    }

                    // status (0 - не готова, 1 - готова, 2 - в работе)
                    if (status < 0 || status > 2)
                    {
                        errorMessage = $"Один из образцов содержит недопустимое значение поля 'status' (допустимо: 0, 1 или 2).";
                        return false;
                    }

                    // concentrations
                    if (!sample.TryGetProperty("concentrations", out JsonElement concElem) || concElem.ValueKind != JsonValueKind.Object)
                    {
                        errorMessage = "Один из образцов содержит отсутствующий или некорректный блок 'concentrations'.";
                        return false;
                    }

                    bool hasElements = false;
                    foreach (JsonProperty element in concElem.EnumerateObject())
                    {
                        hasElements = true;

                        if (element.Value.ValueKind != JsonValueKind.Number)
                        {
                            errorMessage = $"Элемент '{element.Name}' в 'concentrations' содержит некорректное значение.";
                            return false;
                        }
                    }

                    if (!hasElements)
                    {
                        errorMessage = "Блок 'concentrations' не должен быть пустым.";
                        return false;
                    }
                }

                return true;
            }
            catch (JsonException)
            {
                errorMessage = "Файл не является допустимым JSON.";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Ошибка при обработке файла: " + ex.Message;
                return false;
            }
        }
        private void GenerateMergeReport(string pathToSave, List<Sample> resultSamples)
        {
            DateTime startTime = DateTime.Now;
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string reportFile = Path.Combine(integrationReportsDir, $"Merge_Report_{timestamp}.txt");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("ОТЧЁТ ОПЕРАЦИИ ИНТЕГРАЦИИ БАЗ ДАННЫХ");
                sb.AppendLine($"Дата и время начала: {startTime}");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"Первая база: {firstDatabasePath}");
                sb.AppendLine($"Вторая база: {secondDatabasePath}");
                sb.AppendLine($"Сохранённый файл: {pathToSave}");
                sb.AppendLine($"Общее количество объединённых записей: {resultSamples.Count}");
                sb.AppendLine(new string('-', 50));

                var bySurvey = resultSamples.GroupBy(s => s.SurveyId).OrderBy(g => g.Key);
                foreach (var group in bySurvey)
                {
                    sb.AppendLine($"Съёмка ID: {group.Key}, записей: {group.Count()}");
                }

                sb.AppendLine(new string('-', 50));
                sb.AppendLine("История добавления записей:");
                foreach (var sample in resultSamples)
                {
                    sb.AppendLine($"{DateTime.Now:HH:mm:ss} — добавлена запись (ID пробы: {sample.Id}, ID съёмки: {sample.SurveyId})");
                }

                DateTime endTime = DateTime.Now;
                TimeSpan duration = endTime - startTime;

                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"Дата и время завершения: {endTime}");
                sb.AppendLine($"Длительность операции: {duration.TotalSeconds:F2} сек.");
                sb.AppendLine("Завершено успешно.");
                File.WriteAllText(reportFile, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании отчёта:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void GenerateExportReport(string pathToSave, List<Sample> filteredSamples, int selectedSurveyId)
        {
            DateTime startTime = DateTime.Now;
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string reportFile = Path.Combine(exportReportsDir, $"Export_Report_{timestamp}.txt");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("ОТЧЁТ ОПЕРАЦИИ ЭКСПОРТА ДАННЫХ");
                sb.AppendLine($"Дата и время начала: {DateTime.Now}");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"База данных для экспорта: {TxtDbPath.Text}");
                sb.AppendLine($"Сохранённый файл: {pathToSave}");
                sb.AppendLine($"Выбранный ID съёмки: {selectedSurveyId}");
                sb.AppendLine($"Общее количество экспортированных записей: {filteredSamples.Count}");
                sb.AppendLine(new string('-', 50));

                sb.AppendLine($"Съёмка ID: {selectedSurveyId}, записей: {filteredSamples.Count}");

                sb.AppendLine(new string('-', 50));
                sb.AppendLine("История экспорта записей:");
                foreach (var sample in filteredSamples)
                {
                    sb.AppendLine($"{DateTime.Now:HH:mm:ss} — экспортирована запись (ID пробы: {sample.Id}, ID съёмки: {sample.SurveyId})");
                }

                DateTime endTime = DateTime.Now;
                TimeSpan duration = endTime - startTime;

                sb.AppendLine(new string('-', 50));
                sb.AppendLine($"Дата и время завершения: {endTime}");
                sb.AppendLine($"Длительность операции: {duration.TotalSeconds:F2} сек.");
                sb.AppendLine("Завершено успешно.");

                File.WriteAllText(reportFile, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании отчёта:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void GenerateOperationErrorReport(string operationName, string databasePath, int? surveyId, Exception exception, DateTime startTime, string reportsDirectory)
        {
            DateTime endTime = DateTime.Now;
            string timestamp = endTime.ToString("yyyy-MM-dd_HH-mm-ss");
            string reportFile = Path.Combine(reportsDirectory, $"Error_Report_{operationName}_{timestamp}.txt");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"ОТЧЁТ ОБ ОШИБКЕ ОПЕРАЦИИ: {operationName.ToUpper()}");
            sb.AppendLine($"Дата начала: {startTime:yyyy-MM-dd}");
            sb.AppendLine($"Время начала: {startTime:HH:mm:ss}");
            sb.AppendLine(new string('-', 50));

            sb.AppendLine($"База данных: {databasePath}");
            sb.AppendLine(new string('-', 50));
            sb.AppendLine("ПОДРОБНОСТИ ОШИБКИ:");
            sb.AppendLine(exception.ToString());

            sb.AppendLine(new string('-', 50));
            sb.AppendLine($"Дата завершения: {endTime:yyyy-MM-dd}");
            sb.AppendLine($"Время завершения: {endTime:HH:mm:ss}");
            sb.AppendLine("Статус: ЗАВЕРШЕНО С ОШИБКОЙ");

            File.WriteAllText(reportFile, sb.ToString(), Encoding.UTF8);
        }
    }
}
