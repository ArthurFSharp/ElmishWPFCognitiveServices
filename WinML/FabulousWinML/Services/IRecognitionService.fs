namespace FabulousWinML.Services

open System.Threading.Tasks
open System.Collections.Generic

type IRecognitionService =
    abstract member OpenImage: unit -> Task<byte[] option>
    abstract member Recognize: byte[] -> Task<IDictionary<string, float>>