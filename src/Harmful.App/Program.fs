namespace Harmful.App

open System.Windows.Data
type ItemConverter() =
    interface IValueConverter with
        member x.Convert(value, ty, parm, culture) =
            match value with
            | :? Harmful.Types.IItem as i -> i.Text :> obj
            | _ -> "test" :> obj// failwith "Not an IItem"
        member x.ConvertBack(value, ty, parm, culture) = failwith "Not Implemented"

module Program =
    open System.Windows
    open System.Windows.Data
    open System.Windows.Input
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
            }
        member x.DoSearch(searchTerm, list:Controls.ListBox) =
            let ctx = System.Windows.Threading.DispatcherSynchronizationContext.Current |> Option.ofObj
            ctx |> Option.map (fun x ->
                async {
                    let! _ = Async.SwitchToThreadPool()
                    let! allItems = providers |> Seq.map (fetchProvider searchTerm) |> Async.Parallel
                    let items = allItems |> Seq.collect id
                    let! _ = Async.SwitchToContext x
                    list.ItemsSource <- items
                    if not <| Seq.isEmpty items then
                        list.SelectedIndex <- 0

                } |> Async.StartImmediate) |> ignore
            ()
//            let t =Async.s allItems
//            let res = allItems :> System.Collections.IEnumerable
//            res

    type State = { item: Types.IItem option }

    let loaded (w:MainWindow) x =
        w.searchBox.Focus() |> ignore
        ()

    type Action =
    | Exec of Types.IItem
    | Move of bool
    | Exit

    let keyDown state (args:KeyEventArgs) =
        match args.Key with
        | Key.Escape -> Application.Current.Shutdown(0)
        | Key.Up
        | Key.Enter -> match (!state).item with
                       | Some i -> printfn "ENTER"
                       | None -> ()
        | _ -> ()


    [<EntryPoint>]
    [<STAThread>]
    let main argv =
        let w = MainWindow()
        let app = Application()

        let opt = { pluginPaths="" }
        let providers = loadProviders opt
        let sp = SearchProvider(providers)
        let state = ref { item = None }
        w.list.ItemsSource <- []
        w.searchBox.TextChanged.Add (fun x -> sp.DoSearch(w.searchBox.Text, w.list))
        w.searchBox.Text <- "case 123456"
        w.searchBox.PreviewKeyDown.Add(keyDown state)
        w.Loaded.Add (loaded w)

        app.Run(w)
