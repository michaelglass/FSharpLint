#r "nuget: Fornax.Core, 0.16.0"

open Config

let customRename (page: string) =
    System.IO.Path.ChangeExtension(page.Replace ("content/", ""), ".html")


let config = {
    Generators = [
        { Script = "page.fsx"
          Trigger =
              OnFilePredicate (fun path ->
                  let path = string path
                  path.StartsWith "content/" && path.EndsWith ".md")
          OutputFile = Custom customRename }
        {Script = "apiref.fsx"; Trigger = Once; OutputFile = MultipleFiles (sprintf "reference/%s.html") }

        {Script = "lunr.fsx"; Trigger = Once; OutputFile = NewFileName "index.json" }
    ]
}
