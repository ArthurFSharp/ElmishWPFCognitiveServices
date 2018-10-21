open Elmish
open Elmish.WPF
open FsXaml
open System

module SentimentService =
    open Newtonsoft.Json
    open FSharp.Data
    
    type InputDocument =
        {
            Language: string
            Id: string
            Text: string
        }

    type InputData = { Documents: seq<InputDocument> }

    type OutputDocument =
        {
            Score: double
            Id: string
        }

    type SentimentResult = { Documents: seq<OutputDocument> }

    let getSentimentsAsync (message) = async {
        let documents : InputData = { Documents = [| { Language = "fr"; Id = Guid.NewGuid().ToString(); Text = message } |] }
        let! json = Http.AsyncRequestString("https://westeurope.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment", 
                                            httpMethod = "POST",
                                            headers = [ "Content-Type", "application/json; charset=utf-8";
                                                "Ocp-Apim-Subscription-Key", "Insert API Key here";
                                                "Accept", "application/json" ], 
                                            body = TextRequest (JsonConvert.SerializeObject(documents)))
        return JsonConvert.DeserializeObject<SentimentResult>(json)
    }

module SentimentApp =
    type Model =
        {
            Message: string
            SentimentValue: double
        }

    type Msg =
        | SetMessage of string
        | AnalyseSentiment
        | SentimentRetrieved of double
        | DoNothing

    let init() = { Message = ""; SentimentValue = 0. }, Cmd.none

    let retrieveSentimentAsync (message) = async {
        let! sentiments = SentimentService.getSentimentsAsync(message)
        let sentiment = sentiments.Documents |> Seq.tryHead
        let sentimentValue = 
            match sentiment with
            | None -> 0.
            | Some s -> s.Score
        return (SentimentRetrieved sentimentValue)
    }

    let update msg model =
        match msg with
        | SetMessage message -> { model with Message = message }, Cmd.none
        | AnalyseSentiment -> model, Cmd.ofAsync retrieveSentimentAsync (model.Message) id (fun ex -> DoNothing)
        | SentimentRetrieved sentiment -> { model with SentimentValue = (sentiment * 100.) }, Cmd.none
        | DoNothing -> model, Cmd.none

    let bindings model dispatch =
        [
            "Message"          |> Binding.twoWay (fun m -> m.Message) (fun v m -> v |> SetMessage)
            "SentimentValue"   |> Binding.oneWay (fun m -> m.SentimentValue)
            "AnalyseSentiment" |> Binding.cmd (fun m -> AnalyseSentiment)
        ]

type MainWindow = XAML<"MainWindow.xaml">

[<EntryPoint; STAThread>]
let main argv =
    Program.mkProgram SentimentApp.init SentimentApp.update SentimentApp.bindings
    |> Program.runWindow (MainWindow())