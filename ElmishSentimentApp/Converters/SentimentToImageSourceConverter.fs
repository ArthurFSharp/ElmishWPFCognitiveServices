namespace ElmishSentimentApp.Converters

open FsXaml
open System.Windows

type SentimentToImageSourceConverter() =
    inherit ConverterBase
        ((fun b _ _ _ ->
            try
                let value : double = unbox b
                match value with
                | 0. -> box ""
                | v when v > 0. && v < 25. -> box "Assets/rage.png"
                | v when v >= 25. && v < 50. -> box "Assets/bad.png"
                | v when v >= 50. && v < 60. -> box "Assets/neutral.png"
                | v when v >= 60. && v < 80. -> box "Assets/ok.png"
                | _ -> box "Assets/good.png"
            with
            | _ -> DependencyProperty.UnsetValue),
         (ConverterBase.NotImplementedConverter))
