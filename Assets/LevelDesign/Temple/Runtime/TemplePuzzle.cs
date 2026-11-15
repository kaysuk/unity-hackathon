using UnityEngine;
namespace TempleDesign {
 public class TemplePuzzle:MonoBehaviour {
  public int[] sequence;public TempleDoor[] targets;public bool requireDocuments,solved;public int progress;
  public void Press(int symbol,TemplePlayer p){if(solved){p.Notify("Механизм уже открыт.");return;}if(requireDocuments&&p.documents.Count<6){p.Notify("Сначала собери шесть документов: они объясняют порядок знаков.");return;}
   if(symbol!=sequence[progress]){progress=0;p.Notify("Неверный знак. Механизм сброшен.");return;}
   progress++;if(progress==sequence.Length){solved=true;foreach(var d in targets){d.locked=false;d.SetOpen(true);}p.Notify("Верная последовательность. Проход открыт.");}else p.Notify("Знак принят: "+progress+" / "+sequence.Length);
  }
 }
}
