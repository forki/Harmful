namespace Harmful

/// Documentation for my library
///
/// ## Example
///
///     let h = Library.hello 1
///     printfn "%d" h
///

module Types =
    type IItem =
        abstract member Text : string
//        abstract member Icon : Unit option
    and Search = Search of string list
    and [<AbstractClass>] IProvider() =
        abstract member Search: Search -> Async<IItem seq>
        abstract member Exec: IItem -> Command
//    and [<AbstractClass>] IProvider<'a when 'a :> IItem>() =
//        inherit IProvider()
//        override x.Search s =
//            async {
//                let! is = x.SearchItems s
//                return is |> Seq.cast<IItem>
//            }
//        abstract member SearchItems: Search -> Async<'a seq>
    and Command = Exec of string list

module Fogbugz =
    open Types
    type CaseItem = { case:int
                      title:string }
    with
        interface IItem with
            member x.Text = sprintf "Case %i: %s" x.case x.title
//            member x.Icon = None

    type ActionITem = { action:string
                        arg:string
                        f: string -> Unit }
    with
        interface IItem with
            member x.Text = sprintf "%s: %s" x.action x.arg

    module Api =
        type LoginProvider = FSharp.Data.XmlProvider<"""<?xml version="1.0" encoding="UTF-8"?><response><token><![CDATA[82ovqq2bb0ulvsjgitaohf8ugu52gs]]></token></response>""">
        type CaseProvider = FSharp.Data.XmlProvider<"""<?xml version="1.0" encoding="UTF-8"?>
    <response>
        <cases count="1">
            <case ixBug="806120" operations="edit,assign,resolve,reply,forward,remind">
                <sTitle><![CDATA[title]]></sTitle>
            </case>
            <case ixBug="123456"/>
        </cases>
    </response>""">
        open FSharp.Data
        type t = { url: string; token: string }
        let from s : t = { url = s; token = "" }

        let login(u:string)(p:string)(api:t) =
            async {
                let! resp = Http.AsyncRequestString(api.url, query=["cmd","logon";"email",u;"password",p])
                let xmlP = LoginProvider.Parse(resp)
                return { api with token = xmlP.Token }
            }
        let search (id:int) (api:t) =
            async {
                let qparams = [ "token", api.token
                                "cmd","search"
                                "q",id.ToString()
                                "cols", "sTitle"
                                "max", "50"]
                let! resp = Http.AsyncRequestString(api.url, query=qparams)
                printf "\nSearch: %A" resp
                let cases = CaseProvider.Parse resp
                return seq { for c in cases.Cases.Cases do
                                 yield { case = c.IxBug
                                         title = defaultArg c.STitle "" }
                }
            }

    type Provider() =
        inherit IProvider()

        override x.Search(Search tokens) : Async<seq<IItem>> =
            async {
                return seq {
                    match tokens with
                    | "case" :: s :: []  ->
                        match System.Int32.TryParse s with
                        | true,i -> yield { case = i; title = "asd" } :> IItem
                        | _ -> ()
                    | _ -> ()
                    yield { action="Search"; arg = tokens.Head; f = fun _ -> () } :> IItem
                }
            }
        override x.Exec i =
            match i with
            | :? CaseItem as ci -> Exec [sprintf "http://fogbugz.unity3d.com/default.asp?%i" ci.case ]

//module Caching =
//    open System.Runtime.Caching
//
//    type t = private { impl: MemoryCache }
//    let empty (name:string) = { impl = new MemoryCache(name) }
//    let add k v t = t.impl.

module Library =

  /// Returns 42
  ///
  /// ## Parameters
  ///  - `num` - whatever
  let hello num = 42
