module FSharpLint.Core.Tests.TestConfiguration

open NUnit.Framework
open FSharpLint.Framework.Configuration

type System.String with
    member this.ToPlatformIndependentPath() =
        this.Replace('\\', System.IO.Path.DirectorySeparatorChar)

let configWithHints hints =
     { Configuration.Zero with Hints = hints }


[<TestFixture>]
type TestConfiguration() =
    [<Test>]
    member _.``Ignore all files ignores any given file.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "*" ]

        let path = @"D:\dog\source.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths path
        |> Assert.IsTrue

    [<Test>]
    member _.``Ignoring a file name not inside a path does not ignore the path``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "cat" ]

        let path = @"D:\dog\source.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths path
        |> Assert.IsFalse

    [<Test>]
    member _.``Ignoring a file doesn't ignore a directory.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "dog" ]

        let path = @"D:\dog\source.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths  path
        |> Assert.IsFalse

    [<Test>]
    member _.``Ignoring a directory doesn't ignore a file.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "source.fs/" ]

        let path = @"D:\dog\source.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths path
        |> Assert.IsFalse

    [<Test>]
    member _.``Ignoring all files in a given directory ignores a given file from the directory.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "dog/*" ]

        let path = @"D:\dog\source.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths path
        |> Assert.IsTrue

    [<Test>]
    member _.``Ignoring a file that does not exist inside a directory that does exist does not ignore the file.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "dog/source1" ]

        let path = @"D:\dog\source.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths path
        |> Assert.IsFalse

    [<Test>]
    member _.``Ignoring the contents of a directory and then negating a specific file ignores all files other than the negated file.``() =
        let ignorePaths =
            [ IgnoreFiles.parseIgnorePath "dog/*"
              IgnoreFiles.parseIgnorePath "!source.*" ]

        let path = @"D:\dog\source.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths path
        |> Assert.IsFalse

        let path2 = @"D:\dog\source2.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths path2
        |> Assert.IsTrue

    [<Test>]
    member _.``Ingoring a file that was previously negated ignores the file.``() =
        let ignorePaths =
            [ IgnoreFiles.parseIgnorePath "dog/*"
              IgnoreFiles.parseIgnorePath "!source.*"
              IgnoreFiles.parseIgnorePath "dog/*" ]

        let path = @"D:\dog\source.fs".ToPlatformIndependentPath()

        IgnoreFiles.shouldFileBeIgnored ignorePaths path
        |> Assert.IsTrue

    [<Test>]
    member _.``A ** segment matches any number of directories.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "**/obj/**/*.fs" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"obj\Debug\net10.0\X.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\src\App\obj\Debug\net10.0\X.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\X.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\net10.0\gen\X.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

    [<Test>]
    member _.``A ** segment matches zero directories.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "**/obj/**/*.fs" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"obj\X.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored [ IgnoreFiles.parseIgnorePath "dog/**/source.fs" ] (@"D:\dog\source.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

    [<Test>]
    member _.``A leading ** matches a file at any depth.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "**/source.fs" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths ("source.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\a\b\c\source.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\a\b\c\other.fs".ToPlatformIndependentPath())
        |> Assert.IsFalse

    [<Test>]
    member _.``A trailing ** matches everything inside the directory but not a file of that name.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "dog/**" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\dog\source.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\dog\a\b\source.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\cat\dog".ToPlatformIndependentPath())
        |> Assert.IsFalse

    [<Test>]
    member _.``A ** segment still requires the segments around it to match.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "**/obj/**/*.fs" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\src\App\Program.fs".ToPlatformIndependentPath())
        |> Assert.IsFalse

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\X.fsi".ToPlatformIndependentPath())
        |> Assert.IsFalse

    [<Test>]
    member _.``A single * matches exactly one directory.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "obj/*/*.fs" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\X.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\X.fs".ToPlatformIndependentPath())
        |> Assert.IsFalse

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\net10.0\X.fs".ToPlatformIndependentPath())
        |> Assert.IsFalse

    [<Test>]
    member _.``Two stars inside a segment are a plain wildcard, not a globstar.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "dog/a**/source.fs" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\dog\abc\source.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\dog\abc\d\source.fs".ToPlatformIndependentPath())
        |> Assert.IsFalse

    [<Test>]
    member _.``A ** in a directory pattern ignores files under any matching directory.``() =
        let ignorePaths = [ IgnoreFiles.parseIgnorePath "**/obj/**/net10.0/" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\net10.0\X.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\net10.0\sub\X.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\net10.0".ToPlatformIndependentPath())
        |> Assert.IsFalse

    [<Test>]
    member _.``A negated ** pattern un-ignores files at any depth.``() =
        let ignorePaths =
            [ IgnoreFiles.parseIgnorePath "**/obj/**/*.fs"
              IgnoreFiles.parseIgnorePath "!**/obj/**/Keep.fs" ]

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\net10.0\Keep.fs".ToPlatformIndependentPath())
        |> Assert.IsFalse

        IgnoreFiles.shouldFileBeIgnored ignorePaths (@"D:\repo\obj\Debug\net10.0\Drop.fs".ToPlatformIndependentPath())
        |> Assert.IsTrue

    [<Test>]
    member _.``Camel case JSON config correctly parsed into expected config records`` () =
        let expectedConfig =
            { Configuration.Zero with NoTabCharacters = Some { Enabled = true; Config = None } }


        let config =
            """{
    "noTabCharacters": {
        "enabled": true
    }
}
"""
        let actualConfig = parseConfig config

        Assert.AreEqual(expectedConfig.NoTabCharacters, actualConfig.NoTabCharacters)