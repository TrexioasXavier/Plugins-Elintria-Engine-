// ─────────────────────────────────────────────────────────────────────────────
//  BspStudioModel - how a Half-Life model (.mdl) on this object looks and moves
//
//  The sequence it plays, its body group number (a scientist's head, a
//  grunt's gun) and skin. The editor shows the object this way in the Scene
//  view; in Play mode the monster's behaviour drives it, or a script does:
//
//      GetComponent<BspStudioModel>().Play("walk");
//
//  lightColor is the light the map has where the model stands (sampled from
//  the lightmap below it on import): models are drawn with it, as the game
//  lights them from the floor's light.
// ─────────────────────────────────────────────────────────────────────────────
using Elintria;

public class BspStudioModel : ElintriaScript
{
    public string sequence = "";
    public int body;
    public int skin;
    public float frameRate = 1f;
    public bool loop = true;
    /// <summary>Plays the sequence in the Scene view as well as in Play mode.</summary>
    public bool animateInEditor;
    public Vector3 lightColor = new Vector3(0.6f, 0.6f, 0.6f);

    SkinnedModel m_model;
    public SkinnedModel Model => m_model ?? (m_model = SkinnedModel.Get(gameObject));

    public override void Start()
    {
        Model.SetLook(body, skin);
        if (!string.IsNullOrEmpty(sequence)) Model.Play(sequence, frameRate, loop, 0f);
    }

    /// <summary>Plays |seq| (a sequence name), cross-fading.</summary>
    public bool Play(string seq, bool looping = true, float speed = 1f, float blend = 0.15f)
    {
        if (string.IsNullOrEmpty(seq)) return false;
        bool ok = Model.Play(seq, speed * frameRate, looping, blend);
        if (ok) { sequence = seq; loop = looping; }
        return ok;
    }

    public void SetLook(int newBody, int newSkin)
    {
        body = newBody;
        skin = newSkin;
        Model.SetLook(body, skin);
    }

    /// <summary>The model's sequences, by name.</summary>
    public string[] Sequences => Model.clips;
    public bool Finished => Model.finished;
    public float GroundSpeed => Model.groundSpeed;
}
