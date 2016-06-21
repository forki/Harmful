namespace Harmful.App

open System.Windows.Data
type ItemConverter() =
    interface IValueConverter with
        member x.Convert(value, ty, parm, culture) =
            match value with
            | :? Harmful.Types.IItem as i -> i.Text :> obj
            | _ -> "test" :> obj// failwith "Not an IItem"
        member x.ConvertBack(value, ty, parm, culture) = failwith "Not Implemented"

module Arch =
    open System
    open Redux
    open Harmful
    open Harmful.Types

    type State = { selectedIndex: int
                   items: IItem array
                   providers: IProvider list }
    with
        static member empty providers = { selectedIndex = -1; items = Array.empty; providers = providers }


    type Action =
    | Exec
    | Move of bool
    | Search of string
    | Result of IItem seq

    with interface IAction

    let fetchProviders (searchTerm:string) (providers:Types.IProvider list) =
        let fetchProvider (s:string) (p:Types.IProvider) =
            async {
                let! a = p.Search (Types.Search (List.ofArray <| s.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)))
                return a |> Array.ofSeq
            }
        async {
            let! allItems = providers |> List.map (fetchProvider searchTerm) |> Async.Parallel
            return allItems |> Seq.collect id
        }

    let reducer dispatch state (action:IAction) =
        match action with
        | :? Action as a ->
            match a with
            | Exec ->
                Array.tryItem state.selectedIndex state.items
                |> Option.map (fun x -> state.providers |> List.map (fun p -> p.Exec x))
                |> Option.iter (List.iter Harmful.Commands.exec)
                state
            | Move i ->
                let l = Array.length state.items
                if l = 0 then state
                else { state with selectedIndex = (state.selectedIndex + (if i then -1 else 1) + l) % l }
            | Search s ->
                async {
                    let! _ = Async.SwitchToThreadPool()
                    let! items = fetchProviders s state.providers
                    dispatch (Result items)
                } |> Async.StartImmediate
                state
            | Result items -> { state with items = Array.ofSeq items
                                           selectedIndex = 0 }
            | _ -> state
        | _ -> state

module Program =
    open System.Windows
    open System.Windows.Data
    open System.Windows.Input
    open System.Reactive.Linq
    open FSharp.Control.Reactive
    open Harmful
    open Harmful.Fogbugz
    open System
    open FsXaml
    open Redux

    type MainWindow = XAML<"MainWindow.xaml">

    let render (w:MainWindow) (x:Arch.State) =
        printfn "RENDER %i" System.Threading.Thread.CurrentThread.ManagedThreadId
        w.list.ItemsSource <- x.items
        w.list.SelectedIndex <- x.selectedIndex
        ()

    type App(providers) =
        inherit Application()
        static let mutable ctx : System.Threading.SynchronizationContext = null
        static member Cur = App.Current :?> App
        static member Ctx with get () = ctx
        static member init nctx = ctx <- nctx
        member val store:IStore<Arch.State> = Store<Arch.State>(Reducer(Arch.reducer App.dispatch), initialState=Arch.State.empty providers) :> IStore<Arch.State>
        static member dispatch (a:Arch.Action) = App.Cur.store.Dispatch(a) |> ignore

    type Options = { pluginPaths:string }
    let loadProviders (opt:Options) : Types.IProvider list =
        let c = { user = "theor@unity3d.com"
                  apiUrl ="http://fogbugz.unity3d.com/api.asp"
                  password = (System.Environment.GetEnvironmentVariable "FBZPASS") }
        let p = Fogbugz.Provider(c)// :> Types.IProvider
        [ p ]


        
    let keyDown (args:KeyEventArgs) =
        match args.Key with
        | Key.Escape -> Application.Current.Shutdown(0)
        | Key.Up -> App.dispatch(Arch.Move true)
        | Key.Down -> App.dispatch(Arch.Move false)
        | Key.Enter -> App.dispatch Arch.Exec
        | _ -> ()

    let loaded (w:MainWindow) x =
        printfn "UI %i" System.Threading.Thread.CurrentThread.ManagedThreadId
        let ctx = System.Windows.Threading.DispatcherSynchronizationContext.Current
        App.init ctx
        w.searchBox.PreviewKeyDown.Add(keyDown)
        w.searchBox.Focus() |> ignore
        let uisched = System.Reactive.Concurrency.Scheduler.CurrentThread
        App.Cur.store
        |> Observable.observeOnContext ctx
        |> Observable.subscribeOnContext ctx
        |> Observable.subscribe (render w) |> ignore

        w.searchBox.TextChanged//.Publish()
        |> Observable.observeOnContext ctx
        |> Observable.subscribeOnContext ctx
        |> Observable.subscribe (fun a -> App.dispatch (Arch.Search w.searchBox.Text))
        |> ignore
        ()

    [<EntryPoint>]
    [<STAThread>]
    let main argv =
        let opt = { pluginPaths="" }
        let providers = loadProviders opt

        let w = MainWindow()
        let app = App(providers)

        w.Loaded.Add (loaded w)
        app.Run(w)
