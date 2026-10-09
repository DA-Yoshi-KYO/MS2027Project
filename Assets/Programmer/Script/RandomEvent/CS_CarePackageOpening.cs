using UnityEngine;

/*
 * ケアパッケージの開封1回分の情報
 * CSO_RandomEventCarePackageが開封時に作り、中身(CSO_CarePackageContent)の各メソッドに渡す
 *
 * 制作者：　中出峻輔
 */

// 開封1回分の情報(どのイベントで、誰が、どこで開けたか と、中身ごとの状態)
public class CS_CarePackageOpening
{
    private readonly CS_RandomEventContext _context;
    private readonly GameObject _opener;
    private readonly Vector3 _position;

    public CS_RandomEventContext context => _context;   // 開催中のイベント(発生地点など)
    public GameObject opener => _opener;                 // 開けたプレイヤー
    public Vector3 position => _position;                // 箱があった位置(床の高さ)

    // 中身が自由に使う状態の入れ物
    public object state { get; set; }

    public CS_CarePackageOpening(CS_RandomEventContext context, GameObject opener, Vector3 position)
    {
        _context = context;
        _opener = opener;
        _position = position;
    }
}
