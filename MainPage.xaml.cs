using System.Diagnostics;

namespace Lab2_OOP_MAUI;

public partial class MainPage : ContentPage
{
    private string _currentXmlPath = string.Empty;
    private string _currentXslHtmlPath = string.Empty;
    private string _currentXslXmlPath = string.Empty;
    private List<StudentRecord> _lastResults = new();
    private readonly XmlAnalyzerContext _analyzer = new(new SaxSearchStrategy());

    public MainPage()
    {
        InitializeComponent();
        InitializeDefaultFiles();
    }

    private void InitializeDefaultFiles()
    {
        var (xmlPath, xslHtmlPath, xslXmlPath) = XmlHelper.EnsureSampleFilesCreated();
        _currentXmlPath = xmlPath;
        _currentXslHtmlPath = xslHtmlPath;
        _currentXslXmlPath = xslXmlPath;

        LoadAttributesIntoPickers(_currentXmlPath);
        FilePathLabel.Text = $"Поточний XML-файл: {_currentXmlPath}";
        OutputEditor.Text = $"Тестові файли створено у папці:\n{XmlHelper.GetWorkFolder()}\n\nОберіть параметри пошуку або натисніть «Пошук».";
    }

    private void LoadAttributesIntoPickers(string xmlPath)
    {
        try
        {
            var attrs = XmlHelper.ExtractAttributes(xmlPath);

            FullNamePicker.ItemsSource = attrs["FullName"];
            FacultyPicker.ItemsSource = attrs["Faculty"];
            DepartmentPicker.ItemsSource = attrs["Department"];
            CoursePicker.ItemsSource = attrs["Course"];
            SubjectPicker.ItemsSource = attrs["SubjectName"];

            FullNamePicker.SelectedIndex = -1;
            FacultyPicker.SelectedIndex = -1;
            DepartmentPicker.SelectedIndex = -1;
            CoursePicker.SelectedIndex = -1;
            SubjectPicker.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            DisplayAlert("Помилка читання атрибутів", ex.Message, "OK");
        }
    }

    private async void OnLoadXmlClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Оберіть вхідний XML-файл"
            });

            if (result != null)
            {
                _currentXmlPath = result.FullPath;
                FilePathLabel.Text = $"Поточний XML-файл: {_currentXmlPath}";
                LoadAttributesIntoPickers(_currentXmlPath);
                OutputEditor.Text = $"Файл «{result.FileName}» успішно завантажено. Атрибути оновлено у списках вибору.";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка відкриття файла", ex.Message, "OK");
        }
    }

    private void OnSearchClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentXmlPath) || !File.Exists(_currentXmlPath))
        {
            DisplayAlert("Увага", "Спочатку оберіть коректний XML-файл!", "OK");
            return;
        }

        IXmlSearchStrategy selectedStrategy = SaxRadio.IsChecked ? new SaxSearchStrategy()
            : DomRadio.IsChecked ? new DomSearchStrategy()
            : new LinqSearchStrategy();

        _analyzer.SetStrategy(selectedStrategy);

        var criteria = new SearchCriteria
        {
            FullName = FullNamePicker.SelectedItem as string,
            Faculty = FacultyPicker.SelectedItem as string,
            Department = DepartmentPicker.SelectedItem as string,
            Course = CoursePicker.SelectedItem as string,
            SubjectName = SubjectPicker.SelectedItem as string,
            Keyword = KeywordEntry.Text?.Trim()
        };

        var sw = Stopwatch.StartNew();
        _lastResults = _analyzer.ExecuteSearch(_currentXmlPath, criteria);
        sw.Stop();

        EventLogger.Instance.Log("Фільтрація", $"Метод: {selectedStrategy.StrategyName}. Параметри: {criteria}. Знайдено: {_lastResults.Count}");

        if (_lastResults.Count == 0)
        {
            OutputEditor.Text = $"[{selectedStrategy.StrategyName}]\nЗгенерований XPath: {criteria.ToXPathQuery()}\nЗа заданими критеріями нічого не знайдено.";
            return;
        }

        OutputEditor.Text = $"Метод аналізу: {selectedStrategy.StrategyName} (Час виконання: {sw.Elapsed.TotalMilliseconds:F3} мс)\n" +
                            $"Динамічний XPath-вираз: {criteria.ToXPathQuery()}\n" +
                            $"Знайдено записів: {_lastResults.Count}\n" +
                            new string('-', 85) + "\n" +
                            string.Join("\n\n", _lastResults.Select(r => r.ToString()));
    }

    private void OnShowDomTreeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentXmlPath) || !File.Exists(_currentXmlPath)) return;
        OutputEditor.Text = "Рекурсивний розбір об'єкта XmlDocument за допомогою DOM (RecurseNodes):\n" +
                            new string('-', 85) + "\n" +
                            DomSearchStrategy.ParseXmlTreeRecursively(_currentXmlPath);
    }

    private void OnShowSaxStreamClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentXmlPath) || !File.Exists(_currentXmlPath)) return;
        OutputEditor.Text = "Потоковий розбір через клас XmlTextReader (SAX):\n" +
                            new string('-', 85) + "\n" +
                            SaxSearchStrategy.ParsingWithXmlTextReader(_currentXmlPath);
    }

    private async void OnTransformClicked(object sender, EventArgs e)
    {
        try
        {
            string outputHtml = Path.Combine(XmlHelper.GetWorkFolder(), "Report.html");
            _analyzer.TransformToHtml(_currentXmlPath, _currentXslHtmlPath, outputHtml);

            EventLogger.Instance.Log("Трансформація", $"Збережено у файл {outputHtml}");

            OutputEditor.Text = $"Трансформацію XML в HTML виконано!\nФайл збережено за адресою:\n{outputHtml}";

            bool openInBrowser = await DisplayAlert("Трансформація XML в HTML",
                $"HTML-звіт створено:\n{outputHtml}\n\nВідкрити його у браузері?", "Так", "Ні");

            if (openInBrowser)
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest("HTML Звіт", new ReadOnlyFile(outputHtml)));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка трансформації", ex.Message, "OK");
        }
    }

    private void OnTransformXmlToXmlClicked(object sender, EventArgs e)
    {
        try
        {
            string outputXml = Path.Combine(XmlHelper.GetWorkFolder(), "TransformedStudents.xml");
            _analyzer.TransformXmlToXml(_currentXmlPath, _currentXslXmlPath, outputXml);

            EventLogger.Instance.Log("Трансформація", $"Збережено трансформований XML у файл {outputXml}");

            string transformedContent = File.ReadAllText(outputXml);
            OutputEditor.Text = $"Трансформацію XML в XML виконано!\nФайл: {outputXml}\n" +
                                new string('-', 85) + "\n" +
                                transformedContent;
        }
        catch (Exception ex)
        {
            DisplayAlert("Помилка трансформації XML в XML", ex.Message, "OK");
        }
    }

    private void OnRunLinq2And3Clicked(object sender, EventArgs e)
    {
        OutputEditor.Text = PracticeTasksService.ExecuteLinq2And3();
    }

    private void OnRunMyRootSearchClicked(object sender, EventArgs e)
    {
        string id = string.IsNullOrWhiteSpace(MyRootIdEntry.Text) ? "1" : MyRootIdEntry.Text.Trim();
        OutputEditor.Text = PracticeTasksService.SearchMyRootById(id, SaxRadio.IsChecked, DomRadio.IsChecked);
    }

    private async void OnRunMyRootHtmlClicked(object sender, EventArgs e)
    {
        string htmlPath = PracticeTasksService.TransformMyRootToHtml();
        OutputEditor.Text = $"Завдання 7 виконано! Файл MyRoot.html створено:\n{htmlPath}\n\n{File.ReadAllText(htmlPath)}";

        bool open = await DisplayAlert("Завдання 7", $"Таблицю MyRoot.html створено:\n{htmlPath}\n\nВідкрити у браузері?", "Так", "Ні");
        if (open)
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest("MyRoot HTML", new ReadOnlyFile(htmlPath)));
        }
    }

    private async void OnSaveFilteredClicked(object sender, EventArgs e)
    {
        if (_lastResults.Count == 0)
        {
            await DisplayAlert("Увага", "Спочатку виконайте пошук, щоб отримати відфільтрований фрагмент!", "OK");
            return;
        }

        string format = await DisplayActionSheet("У якому форматі зберегти відфільтровані дані? (Factory Method)", "Скасувати", null, "XML (.xml)", "HTML (.html)");
        if (format == "Скасувати" || string.IsNullOrEmpty(format)) return;

        SaverCreator creator = format.StartsWith("XML") ? new XmlSaverCreator() : new HtmlSaverCreator();
        string ext = format.StartsWith("XML") ? "xml" : "html";
        string savePath = Path.Combine(XmlHelper.GetWorkFolder(), $"FilteredResult.{ext}");

        creator.CreateSaver().Save(savePath, _lastResults);
        EventLogger.Instance.Log("Збереження", $"Збережено відфільтрований фрагмент у файл {savePath}");

        await DisplayAlert("Збережено", $"Відфільтрований фрагмент збережено у файл:\n{savePath}\n\nЛог-файл подій:\n{EventLogger.Instance.LogFilePath}", "OK");
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        FullNamePicker.SelectedIndex = -1;
        FacultyPicker.SelectedIndex = -1;
        DepartmentPicker.SelectedIndex = -1;
        CoursePicker.SelectedIndex = -1;
        SubjectPicker.SelectedIndex = -1;
        KeywordEntry.Text = string.Empty;
        OutputEditor.Text = string.Empty;
        _lastResults.Clear();
    }

    private async void OnHelpClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Допомога користувачу",
            "1. Натисніть «Відкрити XML», щоб обрати свій файл (або використовуйте StudentsSuccess.xml).\n" +
            "2. Оберіть значення атрибутів у випадаючих списках.\n" +
            "3. Оберіть метод аналізу: SAX API, DOM API або LINQ to XML та натисніть «Пошук».\n" +
            "4. Кнопки «XML в HTML» та «XML в XML» виконують XSLT-перетворення.\n" +
            "5. Блок «Практичні завдання з лекції» виконує Завдання 2–7 (стор. 296–297).\n" +
            "6. Кнопка «Clear» очищує всі фільтри та поле результатів.", "Зрозуміло");
    }

    private async void OnAboutClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Про програму",
            "Лабораторна робота №2 з дисципліни «Об'єктно-орієнтоване програмування».\n" +
            "Тема: Робота з документами XML (SAX, DOM, LINQ to XML, XPath, XSLT).\n" +
            "Використані шаблони: Strategy, Factory Method, Singleton.\n" +
            "Виконав: студент 2 курсу, групи К-16, Джалялов Назар.", "OK");
    }

    private async void OnExitClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Підтвердження виходу",
            "Чи дійсно ви хочете завершити роботу з програмою?", "Так", "Ні");

        if (confirm)
        {
            Application.Current?.Quit();
        }
    }
}