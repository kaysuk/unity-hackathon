namespace TempleDesign {
 public class TempleButton:TempleInteractable {public TemplePuzzle puzzle;public int symbol;public override void Use(TemplePlayer p){puzzle.Press(symbol,p);}}
}
