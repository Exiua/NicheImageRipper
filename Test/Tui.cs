using System.ComponentModel;
using System.Data;
using Terminal.Gui;

namespace Test;

public class Tui : Window
{
    private readonly Label _statusLabel;

    public Tui()
    {
        Title = "Terminal GUI Ripper";
        X = 0;
        Y = 1; // Menu bar takes row 0
        Width = Dim.Fill();
        Height = Dim.Fill();
        // ColorScheme = new ColorScheme
        // {
        //     Normal = new Terminal.Gui.Attribute(-1)
        // };
        //ColorScheme.Normal = new Terminal.Gui.Attribute(-1);
        //ThemeManager.Instance.Theme
        // ColorScheme = 
        
        // URL Input and Buttons
        var urlField = new TextField
        {
            Text = "",
            X = 1,
            Y = 1,
            Width = 40,
        };

        var ripButton = new Button
        {
            Text = "Rip",
            X = Pos.Right(urlField) + 2,
            Y = Pos.Top(urlField),
        };

        var pauseButton = new Button
        {
            Text = "Pause",
            X = Pos.Right(ripButton) + 2,
            Y = Pos.Top(urlField),
        };

        _statusLabel = new Label
        {
            Text = "Status: Ready",
            X = Pos.Left(urlField),
            Y = Pos.Bottom(urlField) + 1,
            Width = Dim.Fill(),
        };

        // Adding to the main window
        Add(urlField, ripButton, pauseButton, _statusLabel);

        // Create TabView for multiple tabs
        var tabView = new TabView 
        {
            X = 1,
            Y = Pos.Bottom(_statusLabel) + 1,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 6
        };

        // Tab 1: Log
        var logView = new TextView {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true
        };
        tabView.AddTab(new Tab
        {
            Title = "Logs",
            View = logView
        }, true);

        // Tab 2: Queue
        var queueView = new TextView {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true
        };
        
        tabView.AddTab(new Tab
        {
            Title = "Queue",
            View = queueView
        }, false);

        // Tab 3: History
        var historyListView = new TableView {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        
        var table = new DataTable();
        var columnNames = new[] { "Name", "Url", "Date", "#" };
        foreach (var name in columnNames)
        {
            table.Columns.Add(name);
        }
        
        // var rowNames = new[] { "File1", "http://url.com", "Today", "1" };
        // table.Rows.Add(new TableRow("File1", "http://url.com", "Today", "1"));
        historyListView.Table = new DataTableSource(table);
        tabView.AddTab(new Tab
        {
            Title = "History",
            View = historyListView
        }, false);
        
        // Tab 4: Settings
        var settingsTab = new View();
        var saveFolderLabel = new Label
        {
            Text = "Save Location: /path/to/save",
            X = 0,
            Y = 1,
        };
        
        var browseSaveButton = new Button
        {
            Text = "Browse",
            X = Pos.Right(saveFolderLabel) + 2,
            Y = Pos.Top(saveFolderLabel)
        };
        
        var fileSchemeLabel = new Label
        {
            Text = "Filename Scheme:",
            X = 0,
            Y = Pos.Bottom(saveFolderLabel) + 1
        };
        
        var fileSchemeComboBox = new ComboBox {
            X = Pos.Right(fileSchemeLabel) + 2,
            Y = Pos.Top(fileSchemeLabel),
            Width = 20
        };
        
        fileSchemeComboBox.SetSource<string>(["Original", "Hash", "Chronological"]);
        
        var unzipProtocolLabel = new Label
        {
            Text = "Unzip Protocol:",
            X = 0,
            Y = Pos.Bottom(fileSchemeLabel) + 1
        };
        
        var unzipComboBox = new ComboBox {
            X = Pos.Right(unzipProtocolLabel) + 2,
            Y = Pos.Top(unzipProtocolLabel),
            Width = 20
        };
        
        unzipComboBox.SetSource<string>([ "None", "Extract", "Extract and Delete" ]);
        
        var reripCheckbox = new CheckBox
        {
            Text = "Ask to re-rip URL",
            X = 0,
            Y = Pos.Bottom(unzipProtocolLabel) + 1
        };
        
        var liveUpdateCheckbox = new CheckBox
        {
            Text = "Live update history table",
            X = 0,
            Y = Pos.Bottom(reripCheckbox) + 1
        };
        
        settingsTab.Add(saveFolderLabel, browseSaveButton, fileSchemeLabel, fileSchemeComboBox, unzipProtocolLabel, unzipComboBox, reripCheckbox, liveUpdateCheckbox);
        tabView.AddTab(new Tab
        {
            Title = "Settings",
            View = settingsTab
        }, false);
        
        // Add TabView to the main window
        Add(tabView);
        
        // Set up button actions (dummy actions for now)
        ripButton.Accept += RipButtonAction;
        pauseButton.Accept += PauseButtonAction;
    }
    
    // public static void Entry()
    // {
    //     Application.Init();
    //     var top = Application.Current;
    //
    //     // Main Window
    //     var mainWin = new Window
    //     {
    //         Title = "Terminal GUI Ripper",
    //         X = 0,
    //         Y = 1, // Menu bar takes row 0
    //         Width = Dim.Fill(),
    //         Height = Dim.Fill()
    //     };
    //     top.Add(mainWin);
    //
    //     // URL Input and Buttons
    //     var urlField = new TextField
    //     {
    //         Text = "",
    //         X = 1,
    //         Y = 1,
    //         Width = 40,
    //     };
    //
    //     var ripButton = new Button
    //     {
    //         Text = "Rip",
    //         X = Pos.Right(urlField) + 2,
    //         Y = Pos.Top(urlField),
    //     };
    //
    //     var pauseButton = new Button
    //     {
    //         Text = "Pause",
    //         X = Pos.Right(ripButton) + 2,
    //         Y = Pos.Top(urlField),
    //     };
    //
    //     _statusLabel = new Label
    //     {
    //         Text = "Status: Ready",
    //         X = Pos.Left(urlField),
    //         Y = Pos.Bottom(urlField) + 1,
    //         Width = Dim.Fill(),
    //     };
    //
    //     // Adding to the main window
    //     mainWin.Add(urlField, ripButton, pauseButton, _statusLabel);
    //
    //     // Create TabView for multiple tabs
    //     var tabView = new TabView 
    //     {
    //         X = 1,
    //         Y = Pos.Bottom(_statusLabel) + 1,
    //         Width = Dim.Fill() - 2,
    //         Height = Dim.Fill() - 6
    //     };
    //
    //     // Tab 1: Log
    //     var logView = new TextView {
    //         Width = Dim.Fill(),
    //         Height = Dim.Fill(),
    //         ReadOnly = true
    //     };
    //     tabView.AddTab(new Tab
    //     {
    //         Title = "Logs",
    //         View = logView
    //     }, true);
    //
    //     // Tab 2: Queue
    //     var queueView = new TextView {
    //         Width = Dim.Fill(),
    //         Height = Dim.Fill(),
    //         ReadOnly = true
    //     };
    //     tabView.AddTab(new Tab
    //     {
    //         Title = "Queue",
    //         View = queueView
    //     }, false);
    //
    //     // Tab 3: History
    //     var historyListView = new TableView {
    //         X = 0,
    //         Y = 0,
    //         Width = Dim.Fill(),
    //         Height = Dim.Fill()
    //     };
    //     
    //     var table = new DataTable();
    //     var columnNames = new[] { "Name", "Url", "Date", "#" };
    //     foreach (var name in columnNames)
    //     {
    //         table.Columns.Add(name);
    //     }
    //     
    //     // var rowNames = new[] { "File1", "http://url.com", "Today", "1" };
    //     // table.Rows.Add(new TableRow("File1", "http://url.com", "Today", "1"));
    //     historyListView.Table = new DataTableSource(table);
    //     tabView.AddTab(new Tab
    //     {
    //         Title = "History",
    //         View = historyListView
    //     }, false);
    //
    //     // Tab 4: Settings
    //     var settingsTab = new View();
    //     var saveFolderLabel = new Label
    //     {
    //         Text = "Save Location: /path/to/save",
    //         X = 0,
    //         Y = 1,
    //     };
    //
    //     var browseSaveButton = new Button
    //     {
    //         Text = "Browse",
    //         X = Pos.Right(saveFolderLabel) + 2,
    //         Y = Pos.Top(saveFolderLabel)
    //     };
    //
    //     var fileSchemeLabel = new Label
    //     {
    //         Text = "Filename Scheme:",
    //         X = 0,
    //         Y = Pos.Bottom(saveFolderLabel) + 1
    //     };
    //     
    //     var fileSchemeComboBox = new ComboBox {
    //         X = Pos.Right(fileSchemeLabel) + 2,
    //         Y = Pos.Top(fileSchemeLabel),
    //         Width = 20
    //     };
    //     fileSchemeComboBox.SetSource<string>(["Original", "Hash", "Chronological"]);
    //
    //     var unzipProtocolLabel = new Label
    //     {
    //         Text = "Unzip Protocol:",
    //         X = 0,
    //         Y = Pos.Bottom(fileSchemeLabel) + 1
    //     };
    //     
    //     var unzipComboBox = new ComboBox {
    //         X = Pos.Right(unzipProtocolLabel) + 2,
    //         Y = Pos.Top(unzipProtocolLabel),
    //         Width = 20
    //     };
    //     unzipComboBox.SetSource<string>([ "None", "Extract", "Extract and Delete" ]);
    //
    //     var reripCheckbox = new CheckBox
    //     {
    //         Text = "Ask to re-rip URL",
    //         X = 0,
    //         Y = Pos.Bottom(unzipProtocolLabel) + 1
    //     };
    //
    //     var liveUpdateCheckbox = new CheckBox
    //     {
    //         Text = "Live update history table",
    //         X = 0,
    //         Y = Pos.Bottom(reripCheckbox) + 1
    //     };
    //
    //     settingsTab.Add(saveFolderLabel, browseSaveButton, fileSchemeLabel, fileSchemeComboBox, unzipProtocolLabel, unzipComboBox, reripCheckbox, liveUpdateCheckbox);
    //     tabView.AddTab(new Tab
    //     {
    //         Title = "Settings",
    //         View = settingsTab
    //     }, false);
    //
    //     // Add TabView to the main window
    //     mainWin.Add(tabView);
    //
    //     // Set up button actions (dummy actions for now)
    //     ripButton.Accept += RipButtonAction;
    //     pauseButton.Accept += PauseButtonAction;
    //
    //     // Start the application
    //     Application.Run();
    // }

    private void RipButtonAction(object? sender, HandledEventArgs handledEventArgs)
    {
        _statusLabel.Text = "Status: Ripping...";
    }
    
    private void PauseButtonAction(object? sender, HandledEventArgs handledEventArgs)
    {
        _statusLabel.Text = "Status: Paused";
    }
}