module FSharpLint.Rules.RecordFieldNames

open FSharp.Compiler.Syntax
open FSharpLint.Framework.Ast
open FSharpLint.Framework.Rules
open FSharpLint.Rules.Helper.Naming

let rule config =
    let getIdentifiers (args:AstNodeRuleParams) =
        match args.AstNode with
        | AstNode.TypeSimpleRepresentation (SynTypeDefnSimpleRepr.Record (recordFieldsAndSpreads=recordFields)) ->
            recordFields
            |> List.choose (function
                | SynFieldOrSpread.Field(SynField (idOpt=idOpt)) ->
                    Option.map (fun (identifier: Ident) -> (identifier, identifier.idText, None)) idOpt
                | SynFieldOrSpread.Spread _ -> None)
            |> List.toArray
        | _ -> Array.empty

    { Name = "RecordFieldNames"
      Identifier = Identifiers.RecordFieldNames
      RuleConfig = { NamingRuleConfig.Config = config; GetIdentifiersToCheck = getIdentifiers } }
    |> toAstNodeRule
    |> AstNodeRule