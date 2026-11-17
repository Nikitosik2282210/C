using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibraryProject
{

    // 1. СТАТИЧНІ ІНФРАСТРУКТУРНІ КОМПОНЕНТИ

    public static class IdGenerator
    {
        private static int _currentId = 0;
        public static int GetNextId() => ++_currentId;
    }

    public static class Validator
    {
        public static void EnsurePositive(int value, string propertyName)
        {
            if (value <= 0)
                throw new ArgumentException($"{propertyName} має бути більшою за 0.");
        }

        public static void EnsureNotEmpty(string value, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{propertyName} не може бути порожнім.");
        }
    }

    // 2. БАЗОВІ ТА ДОМЕННІ КЛАСИ

    public abstract class Entity
    {
        public int Id { get; set; }
    }

    public class LibraryItem : Entity
    {
        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                Validator.EnsureNotEmpty(value, "Назва");
                _title = value;
            }
        }
        public bool IsAvailable { get; set; } = true;

        public virtual string GetInfo()
        {
            return $"[ID: {Id}] Назва: '{Title}'";
        }
    }

    public class Book : LibraryItem
    {
        public string Author { get; private set; }
        private int _pages;
        public int Pages
        {
            get => _pages;
            set
            {
                Validator.EnsurePositive(value, "Кількість сторінок");
                _pages = value;
            }
        }

        public Book(string title, string author, int pages)
        {
            Title = title;
            Author = author;
            Pages = pages;
        }

        public override string GetInfo() => $"КНИГА -> {base.GetInfo()}, Автор: {Author}, Сторінок: {Pages}";
    }

    public class Magazine : LibraryItem
    {
        private int _issueNumber;
        public int IssueNumber
        {
            get => _issueNumber;
            set
            {
                Validator.EnsurePositive(value, "Номер випуску");
                _issueNumber = value;
            }
        }

        public Magazine(string title, int issueNumber)
        {
            Title = title;
            IssueNumber = issueNumber;
        }

        public override string GetInfo() => $"ЖУРНАЛ -> {base.GetInfo()}, Випуск №{IssueNumber}";
    }

    public class Reader : Entity
    {
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                Validator.EnsureNotEmpty(value, "Ім'я читача");
                _name = value;
            }
        }
    }

    public class Loan : Entity
    {
        public int ItemId { get; set; }
        public int ReaderId { get; set; }
        public DateTime IssueDate { get; set; }
    }


    // 3. УЗАГАЛЬНЕНИЙ РЕПОЗИТОРІЙ (GENERICS)

    public class Repository<T> where T : Entity
    {
        private readonly List<T> _items = new List<T>();

        public void Add(T item)
        {
            if (item.Id == 0)
            {
                item.Id = IdGenerator.GetNextId();
            }
            _items.Add(item);
        }

        public T GetById(int id)
        {
            return _items.FirstOrDefault(i => i.Id == id);
        }

        public List<T> GetAll()
        {
            return _items;
        }
    }


    // 4. СЕРВІСНИЙ ШАР ТА ГОЛОВНА ПРОГРАМА

    public class LibraryService
    {
        private readonly Repository<LibraryItem> _itemRepo = new Repository<LibraryItem>();
        private readonly Repository<Reader> _readerRepo = new Repository<Reader>();
        private readonly Repository<Loan> _loanRepo = new Repository<Loan>();

        public void AddItem(LibraryItem item) => _itemRepo.Add(item);
        public void AddReader(Reader reader) => _readerRepo.Add(reader);

        // Доступ до даних для модуля звітності
        public List<LibraryItem> GetAllItems() => _itemRepo.GetAll();
        public List<Reader> GetAllReaders() => _readerRepo.GetAll();
        public List<Loan> GetAllLoans() => _loanRepo.GetAll();

        public List<LibraryItem> GetAllAvailableItems()
        {
            return _itemRepo.GetAll().Where(i => i.IsAvailable).ToList();
        }

        public void BorrowItem(int readerId, int itemId)
        {
            var reader = _readerRepo.GetById(readerId);
            var item = _itemRepo.GetById(itemId);

            if (reader == null)
                throw new Exception("Читача з таким ID не знайдено.");
            if (item == null)
                throw new Exception("Матеріал з таким ID не знайдено.");

            if (!item.IsAvailable)
                throw new Exception("Цей матеріал вже виданий іншому читачу!");

            item.IsAvailable = false;
            _loanRepo.Add(new Loan
            {
                ItemId = item.Id,
                ReaderId = reader.Id,
                IssueDate = DateTime.Now
            });
        }
    }


    // 5. МОДУЛЬ ЗВІТНОСТІ ТА ЕКСПОРТУ ДАНИХ (ЛР №2)

    public class ReportException : Exception
    {
        public ReportException(string message, Exception inner) : base(message, inner) { }
    }

    public class ReportService
    {
        private const string Header = "Id;Тип;Назва;Деталі;Статус;Читач;Дата видачі";
        private readonly LibraryService _service;

        public ReportService(LibraryService service)
        {
            _service = service;
        }

        // Підсумкова статистика
        public string GetSummary()
        {
            var items = _service.GetAllItems();
            int total = items.Count;
            int available = items.Count(i => i.IsAvailable);
            return $"Всього матеріалів: {total}, доступно: {available}, видано: {total - available}";
        }

        // Формує звіт і зберігає його у CSV-файл. Повертає кількість записаних рядків.
        public int ExportToCsv(string path)
        {
            var items = _service.GetAllItems();
            var readers = _service.GetAllReaders();
            var loans = _service.GetAllLoans();

            try
            {
                // UTF-8 з BOM, щоб Excel коректно відображав українські літери
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
                {
                    writer.WriteLine(Header);

                    foreach (var item in items)
                    {
                        string type = "Інше";
                        string details = "";

                        if (item is Book book)
                        {
                            type = "Книга";
                            details = $"Автор: {book.Author}, сторінок: {book.Pages}";
                        }
                        else if (item is Magazine magazine)
                        {
                            type = "Журнал";
                            details = $"Випуск №{magazine.IssueNumber}";
                        }

                        var loan = loans.FirstOrDefault(l => l.ItemId == item.Id);
                        var reader = loan == null ? null : readers.FirstOrDefault(r => r.Id == loan.ReaderId);

                        string status = item.IsAvailable ? "Доступний" : "Виданий";
                        string readerName = reader != null ? reader.Name : "";
                        string issueDate = loan != null ? loan.IssueDate.ToString("yyyy-MM-dd HH:mm") : "";

                        writer.WriteLine(string.Join(";",
                            item.Id, type, Escape(item.Title), Escape(details), status, Escape(readerName), issueDate));
                    }
                }

                return items.Count;
            }
            catch (Exception ex) when (IsIoError(ex))
            {
                throw ConvertException(ex);
            }
        }

        // Зчитує збережений звіт
        public string[] ReadReport(string path)
        {
            try
            {
                return File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception ex) when (IsIoError(ex))
            {
                throw ConvertException(ex);
            }
        }

        // Перевіряє коректність файлу: заголовок і кількість записів
        public bool Verify(string path, out string message)
        {
            string[] lines = ReadReport(path);

            if (lines.Length == 0 || lines[0] != Header)
            {
                message = "Файл пошкоджений: некоректний заголовок.";
                return false;
            }

            int expected = _service.GetAllItems().Count;
            int actual = lines.Length - 1;

            if (actual != expected)
            {
                message = $"Кількість записів не збігається: у файлі {actual}, у системі {expected}.";
                return false;
            }

            message = $"Файл коректний. Записів: {actual}.";
            return true;
        }

        // Екранування значень для CSV
        private static string Escape(string value)
        {
            if (value.Contains(";") || value.Contains("\""))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static bool IsIoError(Exception ex)
        {
            return ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is NotSupportedException;
        }

        private static ReportException ConvertException(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
                return new ReportException("Немає доступу до файлу або директорії.", ex);
            if (ex is DirectoryNotFoundException)
                return new ReportException("Директорію не знайдено.", ex);
            if (ex is FileNotFoundException)
                return new ReportException("Файл не знайдено.", ex);
            if (ex is PathTooLongException)
                return new ReportException("Шлях до файлу занадто довгий.", ex);
            if (ex is ArgumentException || ex is NotSupportedException)
                return new ReportException("Некоректний шлях до файлу.", ex);

            return new ReportException("Помилка вводу/виводу (файл може бути зайнятий іншою програмою).", ex);
        }
    }

    class Program
    {
        static void Main()
        {
            LibraryService service = new LibraryService();
            ReportService reportService = new ReportService(service);


            service.AddItem(new Book("1984", "Джордж Орвелл", 328));
            service.AddItem(new Book("Кобзар", "Тарас Шевченко", 720));
            service.AddItem(new Magazine("Forbes Україна", 12));

            var reader = new Reader();
            reader.Name = "Іван Франко";
            service.AddReader(reader);

            while (true)
            {
                Console.WriteLine("\n=== МЕНЮ БІБЛІОТЕКИ (Пр №2) ===");
                Console.WriteLine("1. Показати всі доступні матеріали");
                Console.WriteLine("2. Оформити видачу матеріалу читачу");
                Console.WriteLine("3. Зберегти звіт у файл (CSV)");
                Console.WriteLine("4. Прочитати та перевірити збережений звіт");
                Console.WriteLine("0. Вийти");
                Console.Write("Ваш вибір: ");

                string choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            var availableItems = service.GetAllAvailableItems();
                            Console.WriteLine("\n--- ДОСТУПНІ МАТЕРІАЛИ ---");
                            foreach (var item in availableItems)
                            {
                                Console.WriteLine(item.GetInfo());
                            }
                            break;

                        case "2":
                            Console.Write("\nВведіть ID читача: ");
                            int readerId = int.Parse(Console.ReadLine());

                            Console.Write("Введіть ID матеріалу (книги/журналу): ");
                            int itemId = int.Parse(Console.ReadLine());

                            service.BorrowItem(readerId, itemId);
                            Console.WriteLine("Успіх! Матеріал видано.");
                            break;

                        case "3":
                            Console.Write("\nВведіть шлях до файлу (Enter = report.csv): ");
                            string savePath = Console.ReadLine();
                            if (string.IsNullOrWhiteSpace(savePath))
                                savePath = "report.csv";

                            int saved = reportService.ExportToCsv(savePath);
                            Console.WriteLine($"Звіт збережено: {Path.GetFullPath(savePath)} (записів: {saved}).");
                            Console.WriteLine(reportService.GetSummary());
                            break;

                        case "4":
                            Console.Write("\nВведіть шлях до файлу (Enter = report.csv): ");
                            string readPath = Console.ReadLine();
                            if (string.IsNullOrWhiteSpace(readPath))
                                readPath = "report.csv";

                            Console.WriteLine("\n--- ВМІСТ ЗВІТУ ---");
                            foreach (var line in reportService.ReadReport(readPath))
                            {
                                Console.WriteLine(line);
                            }

                            string verifyMessage;
                            reportService.Verify(readPath, out verifyMessage);
                            Console.WriteLine(verifyMessage);
                            break;

                        case "0":
                            return;

                        default:
                            Console.WriteLine("Невірний вибір. Спробуйте ще раз.");
                            break;
                    }
                }
                catch (FormatException)
                {
                    Console.WriteLine("ПОМИЛКА: Введіть коректне число!");
                }
                catch (ReportException ex)
                {
                    Console.WriteLine($"ПОМИЛКА ФАЙЛУ: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ПОМИЛКА БІЗНЕС-ЛОГІКИ: {ex.Message}");
                }
            }
        }
    }
}