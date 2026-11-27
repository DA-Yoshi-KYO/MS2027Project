# -*- coding: utf-8 -*-
"""
Anim_Mirror.py  -  アニメーション左右反転ツール (Maya / Python)

命名規則:
    Con_L_xxx  左コントローラ
    Con_R_xxx  右コントローラ
    Con_C_xxx  センターコントローラ
    ※ 上記以外 (Con_ で始まらない / L,R,C が付かない) は処理しません
    ※ Con_L_xxx に対応する Con_R_xxx が無い場合(逆も同様)は無視します

起動 (スクリプトエディタ / Python タブで実行):
    import Anim_Mirror
    Anim_Mirror.open_window()

    ※ コードを書き換えた後は、下の3行で再読み込みして起動
    import importlib, Anim_Mirror
    importlib.reload(Anim_Mirror)
    Anim_Mirror.open_window()

    シェルフにボタンを作る場合:
    Anim_Mirror.add_to_shelf()
"""
import re
import maya.cmds as cmds
import maya.mel as mel

WINDOW_NAME = "animMirrorWin"

# ---------------------------------------------------------------------------
# 設定 (リグに合わせて編集してください)
# ---------------------------------------------------------------------------

# 左右ペア間でコピーする時の符号 (-1 で反転)
PAIR_SIGN = {
    "translateX": -1,
    "rotateY": -1,
    "rotateZ": -1,
}

# センター(Con_C_)をその場で反転する時の符号
CENTER_SIGN = {
    "translateX": -1,
    "rotateY": -1,
    "rotateZ": -1,
}

CON_RE = re.compile(r"^Con_([LRC])_(.+)$")

# 「手を除外」モードで、この名前のコントローラ自身とその階層下を除外する
HAND_ROOT_RE = re.compile(r"^Con_[LR]_Hand$")

# setKeyframe で指定できるタンジェントタイプ
_IN_TYPES = {"auto", "clamped", "fast", "flat", "linear",
             "plateau", "slow", "spline", "stepnext"}
_OUT_TYPES = {"auto", "clamped", "fast", "flat", "linear",
              "plateau", "slow", "spline", "step"}


# ---------------------------------------------------------------------------
# コントローラ収集
# ---------------------------------------------------------------------------
def _is_finger(long_name):
    """Con_L_Hand / Con_R_Hand 自身、またはその階層下(子孫)にあるか"""
    for part in long_name.split("|"):
        if HAND_ROOT_RE.match(part.rpartition(":")[2]):
            return True
    return False


def _collect(exclude_fingers=False):
    """シーン内の Con_ コントローラを L / R / C に分類して返す
    各辞書のキーは (namespace, Con_L_ 等を除いた名前)
    exclude_fingers=True の時は Con_L_Hand / Con_R_Hand 自身とその配下を除外"""
    nodes = cmds.ls("Con_*", "*:Con_*", type="transform", long=True) or []
    left, right, center = {}, {}, {}
    table = {"L": left, "R": right, "C": center}

    for n in nodes:
        if exclude_fingers and _is_finger(n):
            continue
        leaf = n.split("|")[-1]
        ns, _, short = leaf.rpartition(":")
        m = CON_RE.match(short)
        if not m:
            continue
        side, name = m.groups()
        key = (ns, name)
        if key in table[side]:
            cmds.warning("同名のコントローラが複数あります: %s (最初のものを使用)" % leaf)
            continue
        table[side][key] = n
    return left, right, center


# ---------------------------------------------------------------------------
# キーの読み書き
# ---------------------------------------------------------------------------
def _selected_range():
    """タイムスライダの選択範囲。無ければ None"""
    try:
        pb = mel.eval("$tmp = $gPlayBackSlider")
        if cmds.timeControl(pb, q=True, rangeVisible=True):
            return tuple(cmds.timeControl(pb, q=True, rangeArray=True))
    except Exception:
        pass
    return None


def _read(plug, tkw):
    """プラグのキー情報(または静的な値)を読み出す"""
    times = cmds.keyframe(plug, q=True, tc=True, **tkw)
    if times:
        data = {
            "keys": True,
            "times": times,
            "values": cmds.keyframe(plug, q=True, vc=True, **tkw),
            "itt": cmds.keyTangent(plug, q=True, itt=True, **tkw),
            "ott": cmds.keyTangent(plug, q=True, ott=True, **tkw),
            "ia": cmds.keyTangent(plug, q=True, ia=True, **tkw),
            "oa": cmds.keyTangent(plug, q=True, oa=True, **tkw),
            "weighted": bool(cmds.keyTangent(plug, q=True, weightedTangents=True)[0]),
        }
        if data["weighted"]:
            data["iw"] = cmds.keyTangent(plug, q=True, iw=True, **tkw)
            data["ow"] = cmds.keyTangent(plug, q=True, ow=True, **tkw)
        return data

    if cmds.keyframe(plug, q=True, kc=True):
        return {"keys": False, "skip": True}      # 範囲外にキーあり → 触らない
    return {"keys": False, "skip": False, "value": cmds.getAttr(plug)}


def _write(plug, data, sign, tkw):
    """読み出した情報に符号をかけてプラグに書き込む"""
    if data.get("skip"):
        return

    if cmds.keyframe(plug, q=True, kc=True, **tkw):
        cmds.cutKey(plug, clear=True, **tkw)

    if not data["keys"]:
        try:
            cmds.setAttr(plug, data["value"] * sign)
        except Exception as e:
            cmds.warning("setAttr失敗: %s (%s)" % (plug, e))
        return

    weighted = data["weighted"]
    for i, t in enumerate(data["times"]):
        # setKeyframe では fixed などを指定できないため、仮のタイプで作成し、
        # fixed の場合は後段の keyTangent で角度を設定して fixed にする
        itt = data["itt"][i] if data["itt"][i] in _IN_TYPES else "clamped"
        ott = data["ott"][i] if data["ott"][i] in _OUT_TYPES else "clamped"
        cmds.setKeyframe(plug, time=t, value=data["values"][i] * sign,
                         inTangentType=itt, outTangentType=ott)

    if weighted:
        cmds.keyTangent(plug, e=True, weightedTangents=True)

    # fixed タンジェントのみ角度/ウェイトを復元 (それ以外は自動計算)
    for i, t in enumerate(data["times"]):
        kw = {"time": (t, t), "lock": False}
        if data["itt"][i] == "fixed":
            kw["inAngle"] = data["ia"][i] * sign
            if weighted:
                kw["inWeight"] = data["iw"][i]
        if data["ott"][i] == "fixed":
            kw["outAngle"] = data["oa"][i] * sign
            if weighted:
                kw["outWeight"] = data["ow"][i]
        if len(kw) > 2:
            cmds.keyTangent(plug, e=True, **kw)


def _keyable_attrs(node):
    return cmds.listAttr(node, keyable=True, scalar=True) or []


def _mirror_center(node, tkw):
    cache = {}
    for attr in _keyable_attrs(node):
        if CENTER_SIGN.get(attr, 1) != 1:          # 反転する軸だけ処理
            cache[attr] = _read("%s.%s" % (node, attr), tkw)
    for attr, data in cache.items():
        _write("%s.%s" % (node, attr), data, CENTER_SIGN[attr], tkw)


def _swap_pair(a, b, tkw):
    attrs_b = set(_keyable_attrs(b))
    attrs = [x for x in _keyable_attrs(a) if x in attrs_b]
    data_a = {x: _read("%s.%s" % (a, x), tkw) for x in attrs}
    data_b = {x: _read("%s.%s" % (b, x), tkw) for x in attrs}
    for x in attrs:
        s = PAIR_SIGN.get(x, 1)
        _write("%s.%s" % (b, x), data_a[x], s, tkw)    # L → R
        _write("%s.%s" % (a, x), data_b[x], s, tkw)    # R → L


# ---------------------------------------------------------------------------
# メイン処理
# ---------------------------------------------------------------------------
def mirror_animation(selected_only=False, exclude_fingers=False):
    """アニメーションを左右反転する。結果のメッセージ(str)を返す
    exclude_fingers=True : Con_L_Hand / Con_R_Hand 自身とその配下(指)は対象外"""
    left, right, center = _collect(exclude_fingers)
    if not (left or right or center):
        return "Con_ で始まるコントローラが見つかりません"

    sel = None
    if selected_only:
        sel = set(cmds.ls(sl=True, long=True) or [])
        if not sel:
            return "コントローラが選択されていません"

    pairs, centers, ignored = [], [], []

    for key, l in left.items():
        r = right.get(key)
        if r is None:
            ignored.append(l)                      # 対応するRが無い → 無視
            continue
        if sel is not None and l not in sel and r not in sel:
            continue
        pairs.append((l, r))

    for key, r in right.items():
        if key not in left:
            ignored.append(r)                      # 対応するLが無い → 無視

    for key, c in center.items():
        if sel is None or c in sel:
            centers.append(c)

    if not pairs and not centers:
        return "対象のコントローラがありません"

    trange = _selected_range()
    tkw = {"time": trange} if trange else {}

    cmds.undoInfo(openChunk=True, chunkName="MirrorAnimation")
    try:
        for a, b in pairs:
            _swap_pair(a, b, tkw)
        for c in centers:
            _mirror_center(c, tkw)
    finally:
        cmds.undoInfo(closeChunk=True)

    if ignored:
        print("[MirrorAnimation] 対応する左右がないため無視:")
        for n in ignored:
            print("    " + n)

    range_text = "範囲 %g-%g" % trange if trange else "全キー"
    if exclude_fingers:
        range_text += " / 手除外"
    msg = "完了: 左右ペア %d組 / センター %d個 (%s)" % (len(pairs), len(centers), range_text)
    if ignored:
        msg += " / 無視 %d個" % len(ignored)
    print("[MirrorAnimation] " + msg)
    return msg


# ---------------------------------------------------------------------------
# UI
# ---------------------------------------------------------------------------
def open_window():
    """Anim Mirror のウィンドウを開く"""
    if cmds.window(WINDOW_NAME, exists=True):
        cmds.deleteUI(WINDOW_NAME)

    cmds.window(WINDOW_NAME, title="Anim Mirror", widthHeight=(280, 190),
                sizeable=False)
    cmds.columnLayout(adjustableColumn=True, rowSpacing=8,
                      columnAttach=("both", 12))
    cmds.separator(height=6, style="none")
    cmds.text(label="Con_L_ / Con_R_ / Con_C_ のアニメーションを\n左右反転します", align="center")
    sel_cb = cmds.checkBox(label="選択中のコントローラのみ", value=False)
    finger_cb = cmds.checkBox(label="手を除外 (Con_L/R_Hand とその階層下)", value=False)
    status = cmds.text(label="", align="center", wordWrap=True)

    def _on_click(*_):
        only_sel = cmds.checkBox(sel_cb, q=True, value=True)
        no_finger = cmds.checkBox(finger_cb, q=True, value=True)
        msg = mirror_animation(selected_only=only_sel, exclude_fingers=no_finger)
        cmds.text(status, e=True, label=msg)

    cmds.button(label="左右反転", height=36, command=_on_click)
    cmds.showWindow(WINDOW_NAME)


# ---------------------------------------------------------------------------
# シェルフボタン作成 (任意)
# ---------------------------------------------------------------------------
def add_to_shelf():
    shelf = mel.eval("$tmp = $gShelfTopLevel")
    parent = cmds.tabLayout(shelf, q=True, selectTab=True)
    cmds.shelfButton(
        parent=parent,
        label="AnimMirror",
        annotation="アニメーション左右反転ウィンドウを開く",
        image1="commandButton.png",
        sourceType="python",
        command="import importlib, Anim_Mirror\n"
                "importlib.reload(Anim_Mirror)\n"
                "Anim_Mirror.open_window()",
    )


# 旧名称との互換
show = open_window


if __name__ == "__main__":
    open_window()
