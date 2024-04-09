using sr_mml_lang.parser;
using System.Diagnostics;

class Program {
  const string INITIAL_PAK_PATH = "initial.pak";
  const string INITIAL_PAK_COPY_PATH = "initial_copy.pak";
  const string INITIAL_SIZE_PATH = @"files\initial_size.txt";
  const string TEMP_DIR_PATH = @"files\temp";
  const string SCRIPTS_DIR_PATH = @"files\scripts";

  static bool HasInitial => File.Exists(INITIAL_PAK_PATH);
  static bool HasInitialCopy => File.Exists(INITIAL_PAK_COPY_PATH);
  static bool InitialIsUnpacked => Directory.Exists(TEMP_DIR_PATH);
  static bool HasInitialChanges {
    get {
      var info = new FileInfo(INITIAL_PAK_PATH);
      long prevSize = long.Parse(File.ReadAllText(INITIAL_SIZE_PATH));

      return info.Exists && info.Length != prevSize;
    }
  }

  static void Main() {
    while (true) {
      PrintMainMenu();
    }
  }

  static void PrintMainMenu() {
    RunAction("""
      1. Запустить скрипт
      2. Распаковать initial.pak
      3. Восстановить initial.pak
      4. Для разработчиков

      0. Выход
      """,
      [
        () => Environment.Exit(0),
        RunScript,
        () => UnpackInitial(true),
        RecoverInitial,
        PrintForDevs
      ]
    );
  }

  static void RunScript() {
    string? scriptPath = SelectScript();

    if (scriptPath is null || !UnpackInitial()) {
      return;
    }

    PrintTitle();
    Console.WriteLine("Запуск действий над файлами...");

    new Parser().Parse(
      File.ReadAllText(scriptPath),
      name => {
        string path = $"{Path.GetDirectoryName(scriptPath)}\\{name}.mml";
        return !File.Exists(path) ? null : File.ReadAllText(path);
      }
    );

    Success(waitKey: false);
    UpdateFiles();
    WaitKey();
  }

  static void PrintForDevs() {
    RunAction("""
      1. Добавить скрипт

      0. Назад
      """,
      [
        () => {},
        CreateScript
      ]
    );
  }

  static void RunAction(string message, Action[] actions) {
    RunAction(() => Console.WriteLine(message), actions);
  }
  static void RunAction(Action printMessage, Action[] actions) {
    int index = ReadIndex(() => {
      Console.WriteLine("Выберите действие:\n");
      printMessage();
    }, actions.Length);
    actions[index]();
  }

  static void CreateScript() {
    string? name, desc;
    string dirPath;

    PrintTitle();
    Console.Write("Введите имя скрипта: ");
    name = Console.ReadLine();

    Console.Write("Введите описание срипта: ");
    desc = Console.ReadLine();

    if (name is null || name == "") {
      return;
    }
    name = name.Trim().Replace(' ', '_');
    dirPath = Path.Combine(SCRIPTS_DIR_PATH, name);

    if (Directory.Exists(dirPath)) {
      return;
    }

    Directory.CreateDirectory(dirPath);
    File.WriteAllText(Path.Combine(dirPath, "index.mml"), "// скрипт");
    if (desc is not null and "") {
      File.WriteAllText(Path.Combine(dirPath, "description.txt"), desc);
    }

    Console.WriteLine("\nУспешно создано");
    WaitKey();
  }

  static string? SelectScript() {
    string[] scripts = Directory.GetDirectories(SCRIPTS_DIR_PATH);

    int index = ReadIndex(() => {
      Console.WriteLine("Выберите скрипт для исполнения:\n");

      for (int i = 0; i < scripts.Length; ++i) {
        string dirPath = scripts[i];
        string descPath = $"{dirPath}\\description.txt";

        string name = Path.GetFileName(dirPath)!;
        string description = File.Exists(descPath) ? File.ReadAllText(descPath) : "Без описания";

        Console.WriteLine($"{i + 1}. {name}: {description.Trim()}.");
      }

      Console.WriteLine("\n0. Назад");
    }, scripts.Length);

    return index == 0 ? null : $"{scripts[index - 1]}\\index.mml";
  }

  static bool UnpackInitial(bool waitKey = false) {
    PrintTitle();
    Console.WriteLine("Распаковка initial.pak...");

    if (!CheckInitial()) {
      return false;
    }
    if (!HasInitialChanges) {
      return true;
    }
    if (InitialIsUnpacked) {
      Directory.Delete(TEMP_DIR_PATH, true);
    }

    RunWinRAR($"""x "..\..\{INITIAL_PAK_PATH}" @unpack-list.lst "..\temp\" -ibck""");
    CopyInitial(force: true);
    SaveInitialSize();

    Success(waitKey);
    return true;
  }

  static void UpdateFiles() {
    Console.WriteLine("Обновление файлов в архиве...");
    RunWinRAR($"""f "..\..\{INITIAL_PAK_PATH}" "..\temp\" -ibck -r -ep1""");
    SaveInitialSize();
    Success(waitKey: false);
  }

  static void RecoverInitial() {
    PrintTitle();
    Console.WriteLine("Восстановление initial.pak...");

    if (!CheckInitial() || !CheckInitialCopy()) {
      return;
    }

    File.Delete(INITIAL_PAK_PATH);
    File.Copy(INITIAL_PAK_COPY_PATH, INITIAL_PAK_PATH);
    Success();
  }

  static bool CheckInitial() {
    if (!HasInitial) {
      Console.WriteLine(" initial.pak не найден!");
      WaitKey();

      return false;
    }

    return true;
  }

  static void Success(bool waitKey = true) {
    Console.WriteLine("Завершено");

    if (waitKey) {
      WaitKey();
    }
  }

  static void CopyInitial(bool force = false) {
    if (!HasInitial || (HasInitialCopy && !force)) {
      return;
    }

    if (HasInitialCopy) {
      File.Delete(INITIAL_PAK_COPY_PATH);
    }
    File.Copy(INITIAL_PAK_PATH, INITIAL_PAK_COPY_PATH);
  }

  static bool CheckInitialCopy() {
    if (!HasInitialCopy) {
      Console.WriteLine("initial_copy.pak не найден");
      WaitKey();

      return false;
    }

    return true;
  }

  static void WaitKey() {
    Console.Write("\nНажмите любую клавишу");
    Console.ReadKey();
  }

  static void PrintTitle() {
    Console.Clear();
    Console.Write("SnowRunner manual mod loader v0.2 [VerZsuT]\n\n");
  }

  static int ReadIndex(Action printMenu, int max, int min = 0) {
    int index;

    while (true) {
      PrintTitle();
      printMenu();
      Console.Write("\n-> ");

      if (
        int.TryParse(Console.ReadLine(), out index) &&
        index <= max &&
        index >= min
      ) {
        break;
      }
    }

    return index;
  }

  static void RunWinRAR(string args) {
    ProcessStartInfo startInfo;

    if (!HasInitial) {
      return;
    }

    startInfo = new() {
      CreateNoWindow = false,
      UseShellExecute = true,
      FileName = @"WinRAR.exe",
      WorkingDirectory = @".\files\winrar",
      WindowStyle = ProcessWindowStyle.Hidden,
      Arguments = args
    };

    using Process? exeProcess = Process.Start(startInfo);
    exeProcess?.WaitForExit();
  }

  static void SaveInitialSize() {
    FileInfo info;

    if (!HasInitial) {
      return;
    }

    info = new(INITIAL_PAK_PATH);
    File.WriteAllText(INITIAL_SIZE_PATH, info.Length.ToString());
  }
}
