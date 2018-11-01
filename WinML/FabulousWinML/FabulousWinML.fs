// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace FabulousWinML

open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open System.IO
open FabulousWinML.Services

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
    
    let determineIsGoodPrediction (predictions : Map<string, float>) =
        match predictions.TryFind(Config.predictionValue) with
        | Some shark ->
            if shark > 0.75 then Some true else Some false
        | None -> Some false

    let getPrediction (predictions : Map<string, float>) =
        match predictions.TryFind(Config.predictionValue) with
        | Some shark -> shark
        | None -> 0.
        
    let pickImageFileAsync () = async {
        let! filename = recognitionService.OpenImage() |> Async.AwaitTask
        return FilePicked filename
    }
    
    let recognizeModelAsync (filename) (isOffline) = async {
        let! predictions = async {
            match filename with
            | None -> return Map.empty
            | Some stream -> 
                if isOffline then
                    let! predictions = recognitionService.OfflineClassifierRecognize(stream) |> Async.AwaitTask
                    return (predictions |> Utils.toMap)
                else
                    let! predictions = (OnlineClassifier.recognizeAsync stream Config.projectId Config.iterationId)
                    return (predictions |> Utils.toMap)
        }
        return ModelRecognized predictions
    }

    let update msg model =
        match msg with
        | OpenFile -> model, Cmd.ofAsyncMsg (pickImageFileAsync())
        | FilePicked filestream -> { model with Filestream = filestream }, Cmd.none
        | Recognize -> { model with IsShark = None }, Cmd.ofAsyncMsg (recognizeModelAsync(model.Filestream) model.IsOffline)
        | ModelRecognized predictions -> { model with Predictions = predictions; IsShark = determineIsGoodPrediction predictions }, Cmd.none
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
