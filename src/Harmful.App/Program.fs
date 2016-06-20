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
    open Redux

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
    with
        static member empty = { item = None }

    type Action =
    | Exec of Types.IItem
    | Move of bool
    | Exit
    with 
        interface IAction

    let reducer state (action:IAction) =
        match action with
        | :? Action as a ->
            match a with
            | Exit -> Application.Current.Shutdown(0); state
            | Exec i -> state
            | Move i -> state
            | _ -> state
        | _ -> state
    
    type App() =
        inherit Application()
        static member val store:IStore<State> = Store<State>(Reducer reducer, initialState=State.empty) :> IStore<State>
        static member dispatch (a:Action) = App.store.Dispatch(a) |> ignore

    let render x = ()

    let loaded (w:MainWindow) x =
        w.searchBox.Focus() |> ignore
        App.store.Subscribe render |> ignore
        ()


    let keyDown (args:KeyEventArgs) =
        match args.Key with
        | Key.Escape -> App.dispatch Exit
        | Key.Up -> App.dispatch(Move true)
        | Key.Down -> App.dispatch(Move false)
        | Key.Enter -> App.store.GetState().item |> Option.iter (Exec >> App.dispatch)
        | _ -> ()


    [<EntryPoint>]
    [<STAThread>]
    let main argv =
        let w = MainWindow()
        let app = Application()

        let opt = { pluginPaths="" }
        let providers = loadProviders opt
        let sp = SearchProvider(providers)
        w.list.ItemsSource <- []
        w.searchBox.TextChanged.Add (fun x -> sp.DoSearch(w.searchBox.Text, w.list))
        w.searchBox.Text <- "case 123456"
        w.searchBox.PreviewKeyDown.Add(keyDown)
        w.Loaded.Add (loaded w)

        app.Run(w)
