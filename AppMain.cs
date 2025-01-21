using System.Diagnostics;
using System.Drawing.Text;
using System.Globalization;
using System.Security.Principal;
using System.Text.Json;

//
// Windows限定でUnityエディターのフォントを指定できるfontsettings.txtを変更するツール
// ただしUnity2023.1以降では使っても反映されない
//

namespace Takap.Utility
{
    internal class AppMain
    {
        //
        // Const
        // - - - - - - - - - - - - - - - - - - - -

        // 設定ファイル名
        const string SETTING_FILE_NAME = "settings.json";

        //
        // Fields
        // - - - - - - - - - - - - - - - - - - - -

        // JSONの読み取り方法
        readonly static JsonSerializerOptions _jsonOptions = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip, // コメントは無視
            PropertyNameCaseInsensitive = true // 大文字・小文字を区別しない
        };

        //
        // Methods
        // - - - - - - - - - - - - - - - - - - - -

        static void Main(string[] _)
        {
            try
            {
                if (!AuthUtility.IsAdministrator())
                {
                    throw new NotSupportedException("管理者権限で実行してください。");
                }

                Console.WriteLine("[Start]");

                // JSONから定義内容を読み取ってオブジェクトに展開する
                // アセンブリをまとめて発行した場合機能しなくなるのでコメントアウト
                //string jsonPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), SETTING_FILE_NAME);
                string jsonPath = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), SETTING_FILE_NAME);
                if (!File.Exists(jsonPath))
                {
                    throw new FileNotFoundException($"{SETTING_FILE_NAME} が見つかりません。");
                }
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllBytes(jsonPath), _jsonOptions);

                // JSONの基準パスの存在確認
                if (!Directory.Exists(settings.Editor.EditorInstallBasePath))
                {
                    throw new DirectoryNotFoundException($"ディレクトリーが見つかりません。 EditorInstallBasePath={settings.Editor.EditorInstallBasePath}");
                }

                // どのエディターを選んだのか？
                int editorIndex = SelectEditor(settings, out string editorName);

                // どのフォントを選んだのか？
                int fontIndex = SelectFont(settings, out string fontName);

                // ファイルの内容を更新する
                ReplaceFile(settings, editorName, fontName);

                Console.WriteLine("");
                Console.WriteLine("処理成功");
                Console.WriteLine("");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Error]");
                Console.WriteLine($"メッセージ: {ex.Message}, 型: {ex.GetType().Name}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                Console.WriteLine("[END]");
            }
        }

        /// <summary>
        /// どのエディターを対象にするかを選択します。
        /// </summary>
        static int SelectEditor(Settings settings, out string editorName)
        {
            // ユーザーが指定したエディターのインデックス
            int selectedIndex;

            // フォルダ内のエディターをリストアップしてコンソールへ表示する
            Console.WriteLine("");
            Console.WriteLine("対象のバージョンを選択してください。");
            string[] subDirs = Directory.GetDirectories(settings.Editor.EditorInstallBasePath);
            for (int i = 0; i < subDirs.Length; i++)
            {
                string dir = subDirs[i];
                Console.WriteLine($"[{i}] {Path.GetFileName(dir)}");
            }

            // どれを対象にするか選択する
            Console.WriteLine("");
            while (true)
            {
                Console.Write("Index=");
                string input = Console.ReadLine();

                // 入力チェック、選べないものもはじく
                if (!int.TryParse(input, out int result) || result < 0 || result >= subDirs.Length)
                {
                    Console.WriteLine("入力エラー");
                    continue;
                }
                selectedIndex = result;
                editorName = subDirs[selectedIndex];
                break; // 数値を入力するまで抜けられない
            }

            return selectedIndex;
        }

        /// <summary>
        /// フォント名を選択します。
        /// </summary>
        static int SelectFont(Settings settings, out string fontName)
        {
            // ユーザーが指定したエディターのインデックス
            int selectedIndex;

            // システムにインストールされているフォントを取得
            InstalledFontCollection installedFonts = new();

            // 設定の中から使えるフォントだけを選択する
            List<string> fontList = [];
            foreach (string name in settings.Fonts)
            {
                foreach (var value in installedFonts.Families)
                {
                    string engNem = value.GetName(CultureInfo.GetCultureInfo("en-US").LCID); // たぶん英語名しか設定できない
                    if (engNem.Equals(name))
                    {
                        fontList.Add(name);
                        break;
                    }
                }
            }

            // 適用するフォントを選択する
            Console.WriteLine("");
            Console.WriteLine("使用するフォントを選択してください。");
            for (int i = 0; i < fontList.Count; i++)
            {
                string dir = fontList[i];
                Console.WriteLine($"[{i}] {Path.GetFileName(dir)}");
            }

            // どれを対象にするか選択する
            Console.WriteLine("");
            while (true)
            {
                Console.Write("Index=");
                string input = Console.ReadLine();

                // 入力チェック、選べないものもはじく
                if (!int.TryParse(input, out int result) || result < 0 || result >= fontList.Count)
                {
                    Console.WriteLine("入力エラー");
                    continue;
                }
                selectedIndex = result;
                fontName = fontList[selectedIndex];
                break; // 数値を入力するまで抜けられない
            }

            return selectedIndex;
        }

        /// <summary>
        /// フォントの定義を更新します。
        /// </summary>
        static void ReplaceFile(Settings settings, string editorName, string fonrtName)
        {
            string targetFilePath =
                Path.Combine(settings.Editor.EditorInstallBasePath,
                             editorName,
                             settings.Editor.RelativePath);
            if (!File.Exists(targetFilePath))
            {
                throw new FileNotFoundException("ファイルが見つかりません。Path=" + targetFilePath);
            }

            // 処理対象の文字列
            // ------
            // English|Inter-Regular=Inter, Meiryo UI, Verdana, Tahoma
            // 
            // Japanese|default=MS PGothic, Meiryo UI, Verdana, Tahoma, Arial
            // Japanese|Inter-Regular=MS PGothic Regular, Meiryo UI, Verdana, Tahoma
            // Japanese|Inter-SemiBold=MS PGothic Bold, Meiryo UI Bold, Verdana Bold, Tahoma Bold
            // Japanese|Inter-Small=MS PGothic Regular, Meiryo UI, Verdana, Tahoma
            // Japanese|Inter-Italic=MS PGothic Regular, Meiryo UI Italic, Verdana, Tahoma
            // Japanese|Inter-SemiBoldItalic=MS PGothic Bold, Meiryo UI Bold Italic, Verdana, Tahoma
            // ------

            string tempFilePath = Path.GetTempFileName();
            try
            {
                using (StreamWriter sw = new(tempFilePath))
                {
                    foreach (var line in File.ReadAllLines(targetFilePath))
                    {
                        if (!(line.StartsWith("English|") || line.StartsWith("Japanese|")))
                        {
                            sw.WriteLine(line); // 対象行以外はそのまま出力する
                            continue;
                        }

                        // -- 指定フォントに置き換える --

                        // =で左右に2分割する
                        string[] parts = line.Split('=', StringSplitOptions.None);
                        // フォントリストを全部分割する
                        string[] fonts = parts[1].Split(",", StringSplitOptions.None);

                        // 太字
                        if (parts[0].EndsWith("SemiBold") || parts[0].EndsWith("SemiBoldItalic"))
                        {
                            fonts[0] = $"{fonrtName} Bold";
                        }
                        // 通常を明示
                        else if (parts[0].EndsWith("Inter-Regular") || parts[0].EndsWith("Inter-Small") || parts[0].EndsWith("Inter-Italic"))
                        {
                            fonts[0] = $"{fonrtName} Regular";
                        }
                        else
                        {
                            fonts[0] = fonrtName;
                        }

                        // 復元してファイルに書き込み
                        List<string> partsList = [];
                        partsList.AddRange(fonts);
                        sw.WriteLine($"{parts[0]}={string.Join(',', partsList)}");
                    }
                }

                // 一時ファイルと置き換える
                string ext = Path.GetExtension(targetFilePath);
                string dir = Path.GetDirectoryName(targetFilePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(targetFilePath);
                string backuPath = Path.Combine(dir, $"{fileNameWithoutExt}_{DateTime.Now:yyyyMMddHHmmss}{ext}"); // バックアップを作成
                File.Move(targetFilePath, backuPath, true);
                File.Move(tempFilePath, targetFilePath, true); // ここでエラーになることを面倒なので想定しない
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath); // ゴミを削除
                }
            }
        }
    }

    /// <summary>
    /// このアプリの設定を取得します。
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// エディターの設定情報を取得します。
        /// </summary>
        public EditorSettings Editor { get; set; }

        /// <summary>
        /// 使用可能なフォントリストを取得します。
        /// </summary>
        public string[] Fonts { get; set; }

        //
        // InnerTypes
        // - - - - - - - - - - - - - - - - - - - -
        #region...

        public class EditorSettings
        {
            /// <summary>
            /// エディターがインストールされているパスを取得します。
            /// </summary>
            public string EditorInstallBasePath { get; set; }

            /// <summary>
            /// フォルダ内のフォントが記述されている相対パスを取得します。
            /// </summary>
            public string RelativePath { get; set; }
        }

        #endregion
    }

    public static class AuthUtility
    {
        /// <summary>
        /// 現在のユーザーが管理者権限で実行されているかどうかを取得します。
        /// </summary>
        /// <returns>
        /// true: 管理者権限である / false: それ以外
        /// </returns>
        public static bool IsAdministrator()
        {
            if (Debugger.IsAttached)
            {
                Console.WriteLine($"[Warn] デバッガーで実行されているためtrueを返しました。Key={nameof(IsAdministrator)}");
                return true;
            }
            else
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
