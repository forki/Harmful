module Program =
    open System.Windows
    open Harmful
    open System
    open FsXaml

    type Options = { pluginPaths:string }
    let loadProviders (opt:Options) : Types.IProvider list =
        let p = Fogbugz.Provider()// :> Types.IProvider
        [ p ]

    type MainWindow = XAML<"MainWindow.xaml">

    type SearchProvider(providers) =
        let fetchProvider (s:string) (p:Types.IProvider) =
            async {
                let! a = p.Search (Types.Search (List.ofArray <| s.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)))
                return a |> Array.ofSeq
            } |> Async.RunSynchronously
        interface FeserWard.Controls.IIntelliboxResultsProvider with
            member x.DoSearch(searchTerm, maxResults, extraInfo) =
                let allItems = providers |> Seq.collect (fetchProvider searchTerm)
                let res = allItems :> System.Collections.IEnumerable
                res

    [<EntryPoint>]
    [<STAThread>]
    let main argv =
        let w = MainWindow()
        let app = Application()

        let opt = { pluginPaths="" }
        let providers = loadProviders opt
        w.searchBox.DataProvider <- SearchProvider(providers)
        w.searchBox.DisplayedValueBinding <- Data.Binding("title")

        app.Run(w)
