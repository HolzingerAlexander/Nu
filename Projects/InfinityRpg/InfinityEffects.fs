namespace InfinityRpg
open System
open Prime
open Nu
open Nu.Effects

[<RequireQualifiedAccess>]
module Effects =

    let makeSwordStrikeEffect direction =
        let resource =
            match direction with
            | Upward -> Assets.Gameplay.OakSwordStrikeUp
            | Rightward -> Assets.Gameplay.OakSwordStrikeRight
            | Downward -> Assets.Gameplay.OakSwordStrikeDown
            | Leftward -> Assets.Gameplay.OakSwordStrikeLeft

        let animationSprite =
            AnimatedSprite
                (Resource (AssetTag.toPair resource),
                 v2iDup 144, 2, 2, Constants.InfinityRpg.CharacterAnimationActingDelay, Once, FlipNone, [||], Nil)

        { EffectName = "SwordStrike"
          LifetimeOpt = Some (Constants.InfinityRpg.CharacterAnimationActingDelay * 2L)
          Definitions = Map.empty
          Content = animationSprite }