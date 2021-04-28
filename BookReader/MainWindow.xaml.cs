using BookLibrary;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Annotations;
using System.Windows.Annotations.Storage;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BookReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged

    {
        private int _bookReadProcent;

        public int BookReadProcent
        {
            get { return _bookReadProcent; }
            set
            {
                if (value != _bookReadProcent)
                {
                    _bookReadProcent = value;
                    OnPropertyChanged("BookReadProcent");
                }
            }
        }

        protected virtual void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        FileStream stream;


        private SolidColorBrush modeBgColorN;
        private SolidColorBrush modeBgColorD;
        private SolidColorBrush modeFgColorN;
        private SolidColorBrush modeFgColorD;
        private SolidColorBrush currBg;
        private SolidColorBrush currFg;

        private static String currentMode = "DMODE";

        private ObservableCollection<Book> bookList = new ObservableCollection<Book>();

        private BookDAO bookDAO = new BookDAO();

        private Book book;

        private double width = 0;
        private double height = 0;

        private FontDialog fd;
        private ColorDialog cd;


        public MainWindow()
        {
            InitializeComponent();

            fd = new FontDialog();
            cd = new ColorDialog();

            modeBgColorN = new SolidColorBrush(Color.FromArgb(255, 10, 0, 0));
            modeBgColorD = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            modeFgColorN = Brushes.NavajoWhite;
            modeFgColorD = Brushes.Black;

            //U zavisnosti od trenutnog moda podesavamo trenutnu boju teksta i pozadine
            if (currentMode == "DMODE")
            {
                currBg = modeBgColorD;
                currFg = modeFgColorD;
            }
            else
            {
                currBg = modeBgColorN;
                currFg = modeFgColorN;
            }

            //Podesavanje boje teksta i pozadine flowReader-a
            document.Background = currBg;
            document.Foreground = currFg;

            // Read books from file
            try
            {
                bookList = bookDAO.readBooksFromFile<ObservableCollection<Book>>();
            }
            catch (FileNotFoundException) { }
            listboxBooks.ItemsSource = bookList;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                // Установливаем фильтр для расширения файла и расширения файла по умолчанию 
                DefaultExt = ".txt",
                Filter = "Текстовые файлы (*.txt)|*.txt"
            };
            Nullable<bool> result = dlg.ShowDialog();
            // Получить имя выбранного файла и отобразить в текстовом поле
            if (result == true)
            {
                CloseAnnotations();
                // Если книга открыта, сохраняет изменения файл, перед открытием нового
                if (book != null) {
                    saveBookData();
                }
                // Открываем документ
                string filename = dlg.FileName;
                ReadBook(filename);
                if (!checkIfBookExists(dlg.SafeFileName))
                {

                    if (bookList.Count() > 0)
                    {
                        book = new Book(bookList.Last().id + 1, filename, dlg.SafeFileName);
                    }
                    else
                    {
                        book = new Book(1, filename, dlg.SafeFileName);
                    }

                    if (WindowState == WindowState.Maximized)
                    {
                        book.isMaximizedWindow = true;
                    }
                    else if (book.isMaximizedWindow)
                    {
                        WindowState = WindowState.Maximized;
                        leftPanel.Visibility = Visibility.Collapsed;
                        rightPanel.Visibility = Visibility.Collapsed;
                    }

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    timer.Start();
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        FlowDocReader.GoToPage(book.currentPageNum);
                    };
                    FindInDoc();
                    bookList.Add(book);
                }
                else
                {
                    book = findBookByName(dlg.SafeFileName);

                    System.Windows.Application.Current.MainWindow.Width = book.windowWidth;
                    System.Windows.Application.Current.MainWindow.Height = book.windowHeight;
                    if (book.isMaximizedWindow)
                    {
                        WindowState = WindowState.Maximized;
                        leftPanel.Visibility = Visibility.Collapsed;
                        rightPanel.Visibility = Visibility.Collapsed;
                    }
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                    timer.Start();
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        FlowDocReader.GoToPage(book.currentPageNum);
                    };
                    FindInDoc();
                }
                LoadAnnotations(book);
            }
        }

        private void ReadBook(string filename){

            Paragraph paragraph = new Paragraph();
            string text = File.ReadAllText(filename);
            paragraph.Inlines.Add(text);
            document = new FlowDocument(paragraph)
            {
                ColumnWidth = 2000,
                TextAlignment = TextAlignment.Center,
                Background = currBg,
                Foreground = currFg
            };
            FlowDocReader.Document = document;
        }


        private void LoadAnnotations(Book b) {
            // Грузим заметочки
            AnnotationService service = AnnotationService.GetService(FlowDocReader);
            if (service == null)
            {
                string fullBookName = book.name;
                string bookName = fullBookName.Remove(fullBookName.Length - 4);
                string annotationFileName = bookName + b.id.ToString() + ".xml";
                stream = new FileStream(annotationFileName, FileMode.OpenOrCreate);
                service = new AnnotationService(FlowDocReader);
                AnnotationStore store = new XmlStreamStore(stream);
                store.AutoFlush = true;
                service.Enable(store);
            }
        }

        private void CloseAnnotations(){
            AnnotationService service = AnnotationService.GetService(FlowDocReader);
            if (service != null && service.IsEnabled)
            {
                service.Disable();
                stream.Close();
            }
        }

        protected void OnInitialized(object sender, EventArgs e)
        {
            // Грузим заметочки
            if (book != null)
            {
                LoadAnnotations(book);
            }
        }

        protected void OnClosing(object sender, EventArgs e) 
        {
            saveBookData();
        }

        protected void OnClosed(object sender, EventArgs e)
        {
            CloseAnnotations();
        }

        private void saveBookData() {
            width = System.Windows.Application.Current.MainWindow.ActualWidth;
            height = System.Windows.Application.Current.MainWindow.ActualHeight;

            // Получить номер текущей страницы, если книга открыта
            if (book != null)
            {
                int currentPageNumber = FlowDocReader.MasterPageNumber;
                book.currentPageNum = currentPageNumber;
                book.windowWidth = width;
                book.windowHeight = height;
                book.readBook = bookDAO.calcutePercentReadBook(FlowDocReader.PageCount, currentPageNumber);
                BookReadProcent = book.readBook;
            }
            bookDAO.writeBooksToFile<ObservableCollection<Book>>(bookList);
        }

        private void listboxBooks_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listboxBooks.SelectedItem != null)
            {
                saveBookData();

                CloseAnnotations();
                Book item = (Book)listboxBooks.SelectedItem;
                
                book = findBookByName(item.name);

                ReadBook(book.pathToBook);

                System.Windows.Application.Current.MainWindow.Width = book.windowWidth;
                System.Windows.Application.Current.MainWindow.Height = book.windowHeight;
                
                if (book.isMaximizedWindow) {
                    WindowState = WindowState.Maximized;
                    leftPanel.Visibility = Visibility.Collapsed;
                    rightPanel.Visibility = Visibility.Collapsed;
                }

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                timer.Start();
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    FlowDocReader.GoToPage(book.currentPageNum);
                };

                FindInDoc();
                LoadAnnotations(book);
            }
        }
        // Находим книгу с заданным названием
        private Book findBookByName(string name)
        {
            foreach (Book book in bookList)
            {
                if (book.name.Equals(name))
                {
                    return book;
                }
            }
            return null;
        }
        // Проверяем, существует ли книга с таким ID
        private bool checkIfBookExists(string bookName)
        {
            foreach (Book book in bookList)
            {
                if (book.name.Equals(bookName))
                {
                    return true;
                }
            }
            return false;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (book != null)
            {
                fd.ShowColor = true;
                DialogResult dr = fd.ShowDialog();
                if (dr != System.Windows.Forms.DialogResult.Cancel)
                {
                    document.FontFamily = new FontFamily(fd.Font.Name);
                    document.FontSize = fd.Font.Size * 96.0 / 72.0;
                    document.FontWeight = fd.Font.Bold ? FontWeights.Bold : FontWeights.Regular;
                    document.FontStyle = fd.Font.Italic ? FontStyles.Italic : FontStyles.Normal;
                    Color color = (Color)ColorConverter.ConvertFromString(fd.Color.Name);
                    document.Foreground = new SolidColorBrush(color);

                    book.font = fd.Font.Name;
                    book.fontSize = fd.Font.Size;
                    book.fontWeight = fd.Font.Bold;
                    book.fontStyle = fd.Font.Italic;
                    book.foreground = fd.Color.Name;
                }
            }
            else {
                System.Windows.MessageBox.Show("Откройте книгу заново");
            }
        }
        private void FindInDoc() 
        {
            document.FontFamily = new FontFamily(book.font);
            document.FontSize = book.fontSize * 96.0 / 72.0;
            document.FontWeight = book.fontWeight ? FontWeights.Bold : FontWeights.Regular;
            document.FontStyle = book.fontStyle ? FontStyles.Italic : FontStyles.Normal;
            Color color = (Color)ColorConverter.ConvertFromString(book.foreground);
            document.Foreground = new SolidColorBrush(color);
            if (book.background.Contains(","))
            {
                char[] splitters = { ',' };
                string[] Colors = book.background.Split(splitters);
                Color colorBackground = Color.FromArgb(byte.Parse(Colors[0]), byte.Parse(Colors[1]), byte.Parse(Colors[2]), byte.Parse(Colors[3]));
                document.Background = new SolidColorBrush(colorBackground);
            }
            else {
                document.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(book.background));
            }
            FlowDocReader.Find();
        }
        private void BackgroundColorPicker_Click(object sender, RoutedEventArgs e)
        {
            if (book != null)
            {
                cd.FullOpen = true;
                if (cd.ShowDialog() != System.Windows.Forms.DialogResult.Cancel)
                {
                    try
                    {
                        document.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cd.Color.Name));
                        book.background = cd.Color.Name;
                    }
                    catch (FormatException)
                    {
                        Color color = Color.FromArgb(cd.Color.A, cd.Color.R, cd.Color.G, cd.Color.B);
                        document.Background = new SolidColorBrush(color);
                        book.background = cd.Color.A.ToString() + "," + cd.Color.R.ToString() + "," + cd.Color.G.ToString() + "," + cd.Color.B.ToString();
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Откройте книгу заново");
            }
        }

        private void day_night_Click(object sender, RoutedEventArgs e)
        {
            if (currentMode == "DMODE")
            {
                currentMode = "NMODE";
                currBg = modeBgColorN;
                currFg = modeFgColorN;
            }
            else
            {
                currentMode = "DMODE";
                currBg = modeBgColorD;
                currFg = modeFgColorD;
            }
            document.Background = currBg;
            document.Foreground = currFg;
        }
        private void MenuItemDelete_Click(object sender, RoutedEventArgs e)
        {
            if (listboxBooks.SelectedItem != null) {

                Book item = (Book)listboxBooks.SelectedItem;
                book = findBookByName(item.name);
                string fullBookName = book.name;
                string bookName = fullBookName.Remove(fullBookName.Length - 4);
                string annotationFileName = bookName + book.id.ToString() + ".xml";
                bookList.RemoveAt(listboxBooks.SelectedIndex);
                CloseAnnotations();
                File.Delete(annotationFileName);
                document.Blocks.Clear();
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.PrintDialog printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                document.PageHeight = printDialog.PrintableAreaHeight;
                document.PageWidth = printDialog.PrintableAreaWidth;
                // Устанавливаем поле на 1 дюйм
                document.PagePadding = new Thickness(96);
                // Получаем DocumentPaginator FlowDocument
                var paginatorSource = (IDocumentPaginatorSource)document;
                var paginator = paginatorSource.DocumentPaginator;
                // Печать документа
                printDialog.PrintDocument(paginator, "Печать документа");
            }
        }
    }
}

