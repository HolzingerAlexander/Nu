﻿module Nu.Entity
open System
open System.Reflection
open System.Xml.Serialization
open FSharpx
open FSharpx.Lens.Operators
open OpenTK
open TiledSharp
open Nu.Core
open Nu.Serialization
open Nu.Physics
open Nu.Audio
open Nu.Rendering
                        
let getChild childFinder parent (address : Address) =
    match address with
    | [head] -> childFinder head parent
    | _ -> failwith ("Invalid address '" + str address + "'.")

let setChild childAdder parent (address : Address) child =
    match address with
    | [head] -> childAdder head parent child
    | _ -> failwith ("Invalid address '" + str address + "'.")

let getChildSem childFinder childToSem address parent =
    let child = getChild childFinder parent address
    let semantic = childToSem child
    (child, semantic)

let setChildSem childAdder childSemSetter address parent child semantic =
    let child2 = childSemSetter child semantic
    setChild childAdder parent address child2

let getChildSemSem childFinder childToSemSem address parent =
    let child = getChild childFinder parent address
    let (semantic, semantic2) = childToSemSem child
    (child, semantic, semantic2)

let setChildSemSem childAdder childSemSemSetter address parent child semantic semantic2 =
    let child2 = childSemSemSetter child semantic semantic2
    setChild childAdder parent address child2

let getOptChild optChildFinder parent (address : Address) =
    match address with
    | [] -> None
    | [head] ->
        let optChild = optChildFinder head parent
        match optChild with
        | None -> None
        | Some child -> Some child
    | _ :: _ -> None

let setOptChild addChild removeChild parent (address : Address) optChild =
    match address with
    | [head] ->
        match optChild with
        | None -> removeChild head parent
        | Some child -> addChild head parent child
    | _ -> failwith ("Invalid address '" + str address + "'.")

let getOptChildSem optChildFinder childToOptSem parent address =
    let optChild = getOptChild optChildFinder parent address
    match optChild with
    | None -> None
    | Some child ->
        let optSem = childToOptSem child
        match optSem with
        | None -> None
        | Some semantic -> Some (child, semantic)

let setOptChildSem childAdder childRemover childSemSetter optChildSem parent address =
    match optChildSem with
    | None -> setOptChild childAdder childRemover parent address None
    | Some (child, semantic) -> setChildSem childAdder childSemSetter address parent child semantic
    
let getOptChildSemSem optChildFinder childToOptSemSem parent address =
    let optChild = getOptChild optChildFinder parent address
    match optChild with
    | None -> None
    | Some child ->
        let optSemSem = childToOptSemSem child
        match optSemSem with
        | None -> None
        | Some (semantic, semantic2) -> Some (child, semantic, semantic2)

let setOptChildSemSem childAdder childRemover childSemSemSetter optChildSemSem parent address =
    match optChildSemSem with
    | None -> setOptChild childAdder childRemover parent address None
    | Some (child, semantic, semantic2) -> setChildSemSem childAdder childSemSemSetter address parent child semantic semantic2

type [<StructuralEquality; NoComparison; CLIMutable>] Button =
    { IsDown : bool
      UpSprite : Sprite
      DownSprite : Sprite
      ClickSound : Sound }

type [<StructuralEquality; NoComparison; CLIMutable>] Label =
    { LabelSprite : Sprite }

type [<StructuralEquality; NoComparison; CLIMutable>] TextBox =
    { BoxSprite : Sprite
      Text : string
      TextFont : Font
      TextOffset : Vector2
      TextColor : Vector4 }

type [<StructuralEquality; NoComparison; CLIMutable>] Toggle =
    { IsOn : bool
      IsPressed : bool
      OffSprite : Sprite
      OnSprite : Sprite
      ToggleSound : Sound }

type [<StructuralEquality; NoComparison; CLIMutable>] Feeler =
    { IsTouched : bool }

/// An algabraically-closed semantics for game gui elements.
/// A serializable value type.
type [<StructuralEquality; NoComparison>] GuiSemantic =
    | Button of Button
    | Label of Label
    | TextBox of TextBox
    | Toggle of Toggle
    | Feeler of Feeler
 // | ...additional controls
 // | UserDefinedGui of IUserDefinedGui (* this would give us more open gui semantics, but perhaps at the cost of its value semantics...  *)

/// A game gui element.
/// A serializable value type.
type [<StructuralEquality; NoComparison; CLIMutable>] Gui =
    { Position : Vector2
      Depth : single
      Size : Vector2
      GuiSemantic : GuiSemantic }

    static member button =
        { Get = fun this -> match this.GuiSemantic with Button button -> button | _ -> failwith "Gui is not a button."
          Set = fun button this -> { this with GuiSemantic = Button button }}

    static member optButton =
        { Get = fun this -> match this.GuiSemantic with Button button -> Some button | _ -> None
          Set = fun optButton this -> match optButton with None -> failwith "Cannot set semantic to None." | Some button -> { this with GuiSemantic = Button button }}
    
    static member label =
        { Get = fun this -> match this.GuiSemantic with Label label -> label | _ -> failwith "Gui is not a label."
          Set = fun label this -> { this with GuiSemantic = Label label }}

    static member optLabel =
        { Get = fun this -> match this.GuiSemantic with Label label -> Some label | _ -> None
          Set = fun optButton this -> match optButton with None -> failwith "Cannot set semantic to None." | Some label -> { this with GuiSemantic = Label label }}
    
    static member textBox =
        { Get = fun this -> match this.GuiSemantic with TextBox textBox -> textBox | _ -> failwith "Gui is not a textBox."
          Set = fun textBox this -> { this with GuiSemantic = TextBox textBox }}

    static member optTextBox =
        { Get = fun this -> match this.GuiSemantic with TextBox textBox -> Some textBox | _ -> None
          Set = fun optButton this -> match optButton with None -> failwith "Cannot set semantic to None." | Some textBox -> { this with GuiSemantic = TextBox textBox }}
    
    static member toggle =
        { Get = fun this -> match this.GuiSemantic with Toggle toggle -> toggle | _ -> failwith "Gui is not a toggle."
          Set = fun toggle this -> { this with GuiSemantic = Toggle toggle }}

    static member optToggle =
        { Get = fun this -> match this.GuiSemantic with Toggle toggle -> Some toggle | _ -> None
          Set = fun optButton this -> match optButton with None -> failwith "Cannot set semantic to None." | Some toggle -> { this with GuiSemantic = Toggle toggle }}
    
    static member feeler =
        { Get = fun this -> match this.GuiSemantic with Feeler feeler -> feeler | _ -> failwith "Gui is not a feeler."
          Set = fun feeler this -> { this with GuiSemantic = Feeler feeler }}

    static member optFeeler =
        { Get = fun this -> match this.GuiSemantic with Feeler feeler -> Some feeler | _ -> None
          Set = fun optButton this -> match optButton with None -> failwith "Cannot set semantic to None." | Some feeler -> { this with GuiSemantic = Feeler feeler }}

type [<StructuralEquality; NoComparison; CLIMutable>] Block =
    { PhysicsId : Id
      Density : single
      BodyType : BodyType
      Sprite : Sprite }

type [<StructuralEquality; NoComparison; CLIMutable>] Avatar =
    { PhysicsId : Id
      Density : single
      Sprite : Sprite }
      
type [<StructuralEquality; NoComparison; CLIMutable>] TileMap =
    { PhysicsIds : Id list
      Density : single
      TileMapAsset : TileMapAsset
      TmxMap : TmxMap
      TileMapMetadata : Sprite list }

/// An algabraically-closed semantics for game actors.
/// A serializable value type.
type [<StructuralEquality; NoComparison>] ActorSemantic =
    | Block of Block
    | Avatar of Avatar
    | TileMap of TileMap
 // | ...additional actors
 // | UserDefinedActor of IUserDefinedActor (* this would be one way to get open actor semantics, but perhaps at the cost of its value semantics... *)

/// A game actor.
/// A serializable value type.
type [<StructuralEquality; NoComparison; CLIMutable>] Actor =
    { Position : Vector2
      Depth : single
      Size : Vector2
      Rotation : single
      ActorSemantic : ActorSemantic }

    static member block =
        { Get = fun this -> match this.ActorSemantic with Block block -> block | _ -> failwith "Actor is not a block."
          Set = fun block this -> { this with ActorSemantic = Block block }}

    static member optBlock =
        { Get = fun this -> match this.ActorSemantic with Block block -> Some block | _ -> None
          Set = fun optBlock this -> match optBlock with None -> failwith "Cannot set semantic to None." | Some block -> { this with ActorSemantic = Block block }}
          
    static member avatar =
        { Get = fun this -> match this.ActorSemantic with Avatar avatar -> avatar | _ -> failwith "Actor is not a avatar."
          Set = fun avatar this -> { this with ActorSemantic = Avatar avatar }}

    static member optAvatar =
        { Get = fun this -> match this.ActorSemantic with Avatar avatar -> Some avatar | _ -> None
          Set = fun optAvatar this -> match optAvatar with None -> failwith "Cannot set semantic to None." | Some avatar -> { this with ActorSemantic = Avatar avatar }}

    static member tileMap =
        { Get = fun this -> match this.ActorSemantic with TileMap tileMap -> tileMap | _ -> failwith "Actor is not a tileMap."
          Set = fun tileMap this -> { this with ActorSemantic = TileMap tileMap }}

    static member optTileMap =
        { Get = fun this -> match this.ActorSemantic with TileMap tileMap -> Some tileMap | _ -> None
          Set = fun optTileMap this -> match optTileMap with None -> failwith "Cannot set semantic to None." | Some tileMap -> { this with ActorSemantic = TileMap tileMap }}

/// An algabraically-closed semantics for game entities.
/// A serializable value type.
type [<StructuralEquality; NoComparison>] EntitySemantic =
    | Gui of Gui
    | Actor of Actor
 // | Actor3d of Actor3d

/// A game entity.
/// A serializable value type.
type [<StructuralEquality; NoComparison; CLIMutable>] Entity =
    { Id : Id
      Enabled : bool
      Visible : bool
      EntitySemantic : EntitySemantic }

    interface IXmlSerializable with

        member this.GetSchema () = null

        member this.WriteXml writer =
            writer.WriteStartElement typeof<Entity>.Name
            writePublicProperties writer this
            match this.EntitySemantic with
            | Gui gui ->
                writer.WriteElementString ("EntitySemanticType", "Gui")
                writePublicProperties writer gui
                match gui.GuiSemantic with
                | Button button ->
                    writer.WriteElementString ("GuiSemanticType", "Button")
                    writePublicProperties writer button
                | Label label ->
                    writer.WriteElementString ("GuiSemanticType", "Label")
                    writePublicProperties writer label
                | TextBox textBox ->
                    writer.WriteElementString ("GuiSemanticType", "TextBox")
                    writePublicProperties writer textBox
                | Toggle toggle ->
                    writer.WriteElementString ("GuiSemanticType", "Toggle")
                    writePublicProperties writer toggle
                | Feeler feeler ->
                    writer.WriteElementString ("GuiSemanticType", "Feeler")
                    writePublicProperties writer feeler
            | Actor actor ->
                writer.WriteElementString ("EntitySemanticType", "Actor")
                writePublicProperties writer actor
                match actor.ActorSemantic with
                | Block block ->
                    writer.WriteElementString ("ActorSemanticType", "Block")
                    writePublicProperties writer block
                | Avatar avatar ->
                    writer.WriteElementString ("ActorSemanticType", "Avatar")
                    writePublicProperties writer avatar
                | TileMap tileMap ->
                    writer.WriteElementString ("ActorSemanticType", "TileMap")
                    writePublicProperties writer tileMap
            writer.WriteEndElement ()

        // CLOSURE_LEAK: adding entity, actor, or gui types will cause error without compile-time
        // notice in this method!
        member this.ReadXml reader =
            reader.ReadStartElement ()
            readPublicProperties reader this
            let entitySemanticType = reader.ReadElementString ("EntitySemanticType")
            match entitySemanticType with
            | "Gui" ->
                let gui = Activator.CreateInstance typeof<Gui> :?> Gui
                readPublicProperties reader gui
                let guiSemanticType = reader.ReadElementString ("GuiSemanticType")
                match guiSemanticType with
                | "Button" ->
                    let button = Activator.CreateInstance typeof<Button> :?> Button
                    readPublicProperties reader button
                | "Label" ->
                    let label = Activator.CreateInstance typeof<Label> :?> Label
                    readPublicProperties reader label
                | "TextBox" ->
                    let textBox = Activator.CreateInstance typeof<TextBox> :?> TextBox
                    readPublicProperties reader textBox
                | "Toggle" ->
                    let toggle = Activator.CreateInstance typeof<Toggle> :?> Toggle
                    readPublicProperties reader toggle
                | "Feeler" ->
                    let feeler = Activator.CreateInstance typeof<Feeler> :?> Feeler
                    readPublicProperties reader feeler
                | _ -> failwith <| "Invalid GuiSemanticType '" + guiSemanticType + "'."
            | "Actor" ->
                let actor = Activator.CreateInstance typeof<Actor> :?> Actor
                readPublicProperties reader actor
                let actorSemanticType = reader.ReadElementString ("ActorSemanticType")
                match actorSemanticType with
                | "Block" ->
                    let block = Activator.CreateInstance typeof<Block> :?> Block
                    readPublicProperties reader block
                | "Avatar" ->
                    let avatar = Activator.CreateInstance typeof<Avatar> :?> Avatar
                    readPublicProperties reader avatar
                | "TileMap" ->
                    let tileMap = Activator.CreateInstance typeof<TileMap> :?> TileMap
                    readPublicProperties reader tileMap
                | _ -> failwith <| "Invalid ActorSemanticType '" + actorSemanticType + "'."
            | _ -> failwith <| "Invalid EntitySemanticType '" + entitySemanticType + "'."
            reader.ReadEndElement ()

    static member gui =
        { Get = fun this -> match this.EntitySemantic with Gui gui -> gui | _ -> failwith "Entity is not a gui."
          Set = fun gui this -> { this with EntitySemantic = Gui gui }}
    
    static member optGui =
        { Get = fun this -> match this.EntitySemantic with Gui gui -> Some gui | _ -> None
          Set = fun optGui this -> match optGui with None -> failwith "Cannot set Entity.optGui to None." | Some gui -> set gui this Entity.gui }
    
    static member guiButton =
        { Get = fun this -> let gui = get this Entity.gui in (gui, get gui Gui.button)
          Set = fun (gui, button) this -> let newGui = set button Gui.button in set gui this Entity.gui }
    
    static member optGuiButton =
        { Get = fun this ->
            let optGui = get this Entity.optGui
            match optGui with
            | None -> None
            | Some gui ->
                let optButton = get gui Gui.optButton
                match optButton with
                | None -> None
                | Some button -> Some (gui, button)
          Set = fun optGuiButton this ->
            match optGuiButton with
            | None -> failwith "Cannot set Entity.optGui to None."
            | Some guiButton -> set guiButton this Entity.guiButton }
    
    static member guiLabel =
        { Get = fun this -> let gui = get this Entity.gui in (gui, get gui Gui.label)
          Set = fun (gui, label) this -> let newGui = set label Gui.label in set gui this Entity.gui }
    
    static member optGuiLabel =
        { Get = fun this ->
            let optGui = get this Entity.optGui
            match optGui with
            | None -> None
            | Some gui ->
                let optLabel = get gui Gui.optLabel
                match optLabel with
                | None -> None
                | Some label -> Some (gui, label)
          Set = fun optGuiLabel this ->
            match optGuiLabel with
            | None -> failwith "Cannot set Entity.optGui to None."
            | Some guiLabel -> set guiLabel this Entity.guiLabel }
    
    static member guiTextBox =
        { Get = fun this -> let gui = get this Entity.gui in (gui, get gui Gui.textBox)
          Set = fun (gui, textBox) this -> let newGui = set textBox Gui.textBox in set gui this Entity.gui }
    
    static member optGuiTextBox =
        { Get = fun this ->
            let optGui = get this Entity.optGui
            match optGui with
            | None -> None
            | Some gui ->
                let optTextBox = get gui Gui.optTextBox
                match optTextBox with
                | None -> None
                | Some textBox -> Some (gui, textBox)
          Set = fun optGuiTextBox this ->
            match optGuiTextBox with
            | None -> failwith "Cannot set Entity.optGui to None."
            | Some guiTextBox -> set guiTextBox this Entity.guiTextBox }
    
    static member guiToggle =
        { Get = fun this -> let gui = get this Entity.gui in (gui, get gui Gui.toggle)
          Set = fun (gui, toggle) this -> let newGui = set toggle Gui.toggle in set gui this Entity.gui }
    
    static member optGuiToggle =
        { Get = fun this ->
            let optGui = get this Entity.optGui
            match optGui with
            | None -> None
            | Some gui ->
                let optToggle = get gui Gui.optToggle
                match optToggle with
                | None -> None
                | Some toggle -> Some (gui, toggle)
          Set = fun optGuiToggle this ->
            match optGuiToggle with
            | None -> failwith "Cannot set Entity.optGui to None."
            | Some guiToggle -> set guiToggle this Entity.guiToggle }
    
    static member guiFeeler =
        { Get = fun this -> let gui = get this Entity.gui in (gui, get gui Gui.feeler)
          Set = fun (gui, feeler) this -> let newGui = set feeler Gui.feeler in set gui this Entity.gui }
    
    static member optGuiFeeler =
        { Get = fun this ->
            let optGui = get this Entity.optGui
            match optGui with
            | None -> None
            | Some gui ->
                let optFeeler = get gui Gui.optFeeler
                match optFeeler with
                | None -> None
                | Some feeler -> Some (gui, feeler)
          Set = fun optGuiFeeler this ->
            match optGuiFeeler with
            | None -> failwith "Cannot set Entity.optGui to None."
            | Some guiFeeler -> set guiFeeler this Entity.guiFeeler }

    static member actor =
        { Get = fun this -> match this.EntitySemantic with Actor actor -> actor | _ -> failwith "Entity is not an actor."
          Set = fun actor this -> { this with EntitySemantic = Actor actor }}
    
    static member optActor =
        { Get = fun this -> match this.EntitySemantic with Actor actor -> Some actor | _ -> None
          Set = fun optActor this -> match optActor with None -> failwith "Cannot set Entity.optActor to None." | Some actor -> set actor this Entity.actor }
    
    static member actorBlock =
        { Get = fun this -> let actor = get this Entity.actor in (actor, get actor Actor.block)
          Set = fun (actor, block) this -> let newActor = set block Actor.block in set actor this Entity.actor }
    
    static member optActorBlock =
        { Get = fun this ->
            let optActor = get this Entity.optActor
            match optActor with
            | None -> None
            | Some actor ->
                let optBlock = get actor Actor.optBlock
                match optBlock with
                | None -> None
                | Some block -> Some (actor, block)
          Set = fun optActorBlock this ->
            match optActorBlock with
            | None -> failwith "Cannot set Entity.optActor to None."
            | Some actorBlock -> set actorBlock this Entity.actorBlock }
    
    static member actorAvatar =
        { Get = fun this -> let actor = get this Entity.actor in (actor, get actor Actor.avatar)
          Set = fun (actor, avatar) this -> let newActor = set avatar Actor.avatar in set actor this Entity.actor }
    
    static member optActorAvatar =
        { Get = fun this ->
            let optActor = get this Entity.optActor
            match optActor with
            | None -> None
            | Some actor ->
                let optAvatar = get actor Actor.optAvatar
                match optAvatar with
                | None -> None
                | Some avatar -> Some (actor, avatar)
          Set = fun optActorAvatar this ->
            match optActorAvatar with
            | None -> failwith "Cannot set Entity.optActor to None."
            | Some actorAvatar -> set actorAvatar this Entity.actorAvatar }
    
    static member actorTileMap =
        { Get = fun this -> let actor = get this Entity.actor in (actor, get actor Actor.tileMap)
          Set = fun (actor, tileMap) this -> let newActor = set tileMap Actor.tileMap in set actor this Entity.actor }
    
    static member optActorTileMap =
        { Get = fun this ->
            let optActor = get this Entity.optActor
            match optActor with
            | None -> None
            | Some actor ->
                let optTileMap = get actor Actor.optTileMap
                match optTileMap with
                | None -> None
                | Some tileMap -> Some (actor, tileMap)
          Set = fun optActorTileMap this ->
            match optActorTileMap with
            | None -> failwith "Cannot set Entity.optActor to None."
            | Some actorTileMap -> set actorTileMap this Entity.actorTileMap }