using System;
using System.Collections.Generic;
using System.Linq;

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

    class Program
    {
        static void Main()
        {
            LibraryService service = new LibraryService();

            
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
                catch (Exception ex)
                {
                    Console.WriteLine($"ПОМИЛКА БІЗНЕС-ЛОГІКИ: {ex.Message}");
                }
            }
        }
    }
}