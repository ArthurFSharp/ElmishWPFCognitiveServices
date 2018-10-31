// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace FabulousWinML

open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open System.IO
open System
open System.Net.Http
open System.Net.Http.Headers
open Newtonsoft.Json

module App = 
    
    let recognitionService = DependencyService.Get<FabulousWinML.Services.IRecognitionService>()
    
    type Model = 
      { Predictions: Map<string, float>
        Filestream: byte[] option
        IsShark: bool option
        IsOffline: bool }

    type Msg = 
        | OpenFile
        | FilePicked of byte[] option
        | Recognize
        | ModelRecognized of Map<string, float>
        | SwitchConnectionStatus

    let initModel = { Predictions = Map.empty; Filestream = None; IsShark = None; IsOffline = false }

    let init () = initModel, Cmd.none

    let toMap dictionary = 
        (dictionary :> seq<_>)
        |> Seq.map (|KeyValue|)
        |> Map.ofSeq

    let determineIsShark (predictions : Map<string, float>) =
        match predictions.TryFind("shark") with
        | Some shark ->
            if shark > 0.75 then Some true else Some false
        | None -> Some false

    let getPrediction (predictions : Map<string, float>) =
        match predictions.TryFind("shark") with
        | Some shark -> shark
        | None -> 0.
        
    let pickImageFileAsync () = async {
        let! filename = recognitionService.OpenImage() |> Async.AwaitTask
        return FilePicked filename
    }
    
    type ImageTagPrediction = {
        TagId: Guid
        [<JsonProperty("TagName")>] Tag: string
        Probability: float
    }

    type ImagePredictionResult = {
        Id: Guid
        Project: Guid
        Iteration: Guid
        Created: DateTime
        Predictions: seq<ImageTagPrediction>
    }

    let recognizeAsync (stream : byte[]) (projectId : Guid) (iterationId : Guid option) = async {
        let customVisionEndpoint = "https://southcentralus.api.cognitive.microsoft.com/customvision/v2.0/"
        let httpClient = new HttpClient(BaseAddress = new Uri(customVisionEndpoint))
        let iterationIdValue =
            match iterationId with
            | Some value -> value
            | None       -> Guid.Empty
        let endpoint = sprintf "Prediction/%A/image?iterationId=%A" projectId iterationIdValue
        let request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        request.Headers.Add("Prediction-Key", "d8d5c69119fc41f5b24e43eeffd8f4c0")
        let image = new MemoryStream(stream) :> Stream
        request.Content <- new StreamContent(image)
        request.Content.Headers.ContentType <- new MediaTypeHeaderValue("application/octet-stream")
        let! response = httpClient.SendAsync(request) |> Async.AwaitTask
        let! predictions = 
            match response.IsSuccessStatusCode with
            | true -> async {
                        let! responseContentString = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                        return JsonConvert.DeserializeObject<ImagePredictionResult>(responseContentString)
                     }
            | false -> failwith response.ReasonPhrase
        let q = query {
            for p in predictions.Predictions do
            select (p.Tag, p.Probability)
        }
        return q |> Map.ofSeq
    }

    let recognizeModelAsync (filename) (isOffline) = async {
        let! predictions = async {
            match filename with
            | None -> return Map.empty
            | Some stream -> 
                if isOffline then
                    let! predictions = recognitionService.Recognize(stream) |> Async.AwaitTask
                    return (predictions |> toMap)
                else
                    let! predictions = (recognizeAsync stream (Guid.Parse("1323b843-ad67-402f-9331-3a197a6fc6da")) (Some(Guid.Parse("4691212e-83ad-49d2-a674-b07fa8163539"))))
                    return (predictions |> toMap)
        }
        return ModelRecognized predictions
    }

    let update msg model =
        match msg with
        | OpenFile -> model, Cmd.ofAsyncMsg (pickImageFileAsync())
        | FilePicked filestream -> { model with Filestream = filestream }, Cmd.none
        | Recognize -> model, Cmd.ofAsyncMsg (recognizeModelAsync(model.Filestream) model.IsOffline)
        | ModelRecognized predictions -> { model with Predictions = predictions; IsShark = determineIsShark predictions }, Cmd.none
        | SwitchConnectionStatus -> { model with IsOffline = not model.IsOffline }, Cmd.none

    let view (model: Model) dispatch =
        View.NavigationPage(
            barBackgroundColor = Styles.accentColor,
            barTextColor = Styles.accentTextColor,
            pages = 
                [ View.ContentPage(
                      title = "Fabulous WinML",
                      content = View.StackLayout(padding = 20.0, verticalOptions = LayoutOptions.StartAndExpand,
                        children = [ 
                            yield View.StackLayout(orientation = StackOrientation.Horizontal, horizontalOptions = LayoutOptions.Center,
                                children = [
                                    View.Label(text = "Simulate offline mode", verticalOptions = LayoutOptions.Center)
                                    View.Switch(isToggled = model.IsOffline, toggled=(fun args -> dispatch SwitchConnectionStatus))
                            ])
                            yield View.Button(text = "Open file", command = (fun () -> dispatch OpenFile))
                            yield View.Button(text = "Recognize", command = (fun () -> dispatch Recognize), canExecute = (model.Filestream.IsSome))
                            match model.IsShark with
                            | Some value -> match value with
                                            | false -> yield View.Label(text = "This is not a shark")
                                            | true -> yield View.Label(text = sprintf "This is a shark (%s %%)" (((getPrediction model.Predictions) * 100.).ToString("#0.00")))
                            | None -> yield View.Label(text = "")
                            yield View.Image(source = match model.Filestream with
                                                      | None -> ImageSource.FromStream(null)
                                                      | Some stream -> ImageSource.FromStream(fun () -> new MemoryStream(stream) :> Stream))
                        ]
                      )
                  )
                ])
        

    // Note, this declaration is needed if you enable LiveUpdate
    let program = Program.mkProgram init update view

type App () as app = 
    inherit Application ()

    let runner = 
        App.program
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.runWithDynamicView app
