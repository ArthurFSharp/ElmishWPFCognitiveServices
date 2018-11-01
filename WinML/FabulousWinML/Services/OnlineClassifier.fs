namespace FabulousWinML.Services

open System
open System.IO
open System.Net.Http
open System.Net.Http.Headers
open Newtonsoft.Json
open FabulousWinML

module OnlineClassifier =
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
        request.Headers.Add("Prediction-Key", Config.predictionKey)
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