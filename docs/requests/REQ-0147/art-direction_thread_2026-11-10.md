# Export: direct messages, Priya Natarajan ↔ Owen Calder

**Workspace:** Brackenfield Interactive
**Exported:** 2026-11-10 16:52 ET by priya.natarajan
**Attachments in original thread:** `moodboard_v3.pdf` (Owen, 14:21). Not included in this export.

---

**Priya Natarajan** · Tue 10 Nov, 13:58
hey Owen, REQ-0147 is basically through, Jess signed off last night and I just need yours. The pipeline's going to be generating the character models, portraits, store art etc. What should I give it so the stuff actually looks good and not like generic AI soup? I'd rather hand it your direction in your words than me paraphrasing you badly

**Owen Calder** · 14:03
honestly my first instinct is "give it to me and I'll do it" but I know we don't have the weeks for that lol

**Owen Calder** · 14:04
ok give me a bit, I'll dump what I've got. most of it's been in my head since the pitch

**Priya Natarajan** · 14:04
🙏

**Owen Calder** · 14:16
ok overall style first, applies to everything

**Owen Calder** · 14:17
stylised, chunky, toy-like. think painted vinyl figure not realistic armour. hands and feet a bit big, heads a bit big (not chibi, like 6.5 heads tall)

**Owen Calder** · 14:17
silhouette first. every character has to be identifiable as a solid black shape at icon size. if you cant tell them apart in silhouette its wrong

**Owen Calder** · 14:18
textures should feel hand painted. soft gradients, light baked in from above, minimal noise. NOT photoreal pbr, no grime, no scratches on everything

**Owen Calder** · 14:19
warm world, cool heroes. environment is warm stone/wood, characters carry the saturated colour

**Owen Calder** · 14:19
oh and no weapons. nobody carries a sword. it's a movement controller, I don't want buyers thinking there's combat in the package

**Owen Calder** · 14:21
📎 moodboard_v3.pdf
refs are in here. pages 2-4 are the vibe, page 5 is the palette, ignore page 6 that's from the old pitch

**Owen Calder** · 14:33
ok characters, same order as the doc

**Owen Calder** · 14:33
KNIGHT. she's the face, she's on everything
- reliable, a bit earnest, a bit dorky. steady not cocky
- rounded helmet with a T visor slit. the helmet IS the silhouette so make it slightly oversized. small brass crest on top, not a plume
- teal plate #1F5C5A, brass trim #C89B3C, off-white tabard #EDE6D6 with a simple brass circle emblem. no letters or text on it
- no cape. capes are a nightmare to animate and clip like crazy
- small round shield on her back. optional, cut it if it causes any problems at all
- normal proportions, same height as the current mannequin

**Owen Calder** · 14:34
SCOUT
- quick, curious, bit cheeky. I've been drawing her as the youngest of the three
- leaner and a bit shorter than the knight
- hooded shoulder cloak, rust orange #C4562B. hood DOWN by default so we see her face. it stops above the waist so its not a cape, its fine
- leather straps + pouches #6B4A2F, goggles pushed up on her forehead, long scarf (can be short if long is a problem)
- light soft boots, she should look like she makes no noise

**Owen Calder** · 14:35
HEAVY
- big, calm, gentle. NOT a brute. kind blacksmith, or the guy who works the mines and is nice to everyone
- noticeably broader. taller too if the rig allows
- slate grey #4A4F57 with ember orange #E07A2E accents, huge rounded pauldrons, bare forearms with cloth wraps, heavy leather apron, big satchel on his hip
- small head relative to the shoulders, for the silhouette

**Owen Calder** · 14:36
re "heavy reuses the knight's model": I saw that in the doc. I get why Jess wants it, but if he looks like the knight in a bigger suit he reads as a recolour, and buyers notice recolours. he should read as a different person. if the tools can genuinely bulk the knight up into something that looks like ^ then fine, I don't really know how these tools work

**Priya Natarajan** · 14:38
noted, I'll flag it to Jess. it's Q-01 on the doc already

**Priya Natarajan** · 14:41
props? interactions need doors, levers, crates and the relic

**Owen Calder** · 14:47
door: chunky wooden planks, two iron bands, ring handle. warm wood. the frame is level geo, not part of the prop

**Owen Calder** · 14:48
lever: floor lever, waist height, thick wooden handle on an iron base. they pull it with both hands so the handle needs to be wide enough to read

**Owen Calder** · 14:49
crates: small = classic wooden crate, about knee height. large = same family but reinforced, iron corners, about chest height, should look HEAVY

**Owen Calder** · 14:50
relic: palm sized brass orrery-ish thing, glowing teal core. same teal as the knight, which is why she's holding it in the key art. it's a mcguffin. small and precious, not a giant glowing artefact

**Priya Natarajan** · 14:52
the door handle height matters for the IK reach, I'll need to know where it is on the mesh

**Owen Calder** · 14:53
normal door handle height, ~1m? you tell me, you're the IK person

**Priya Natarajan** · 15:02
poly/texture budgets? the doc points to you for those

**Owen Calder** · 15:10
ballpark 15-20k tris per character, props 1-3k (relic can go a bit over, it's in the key art). textures 2k max for characters, 1k for props. one material per character if you can, two max

**Priya Natarajan** · 15:11
and they need to be rigged? I'm assuming yes

**Owen Calder** · 15:12
yeah humanoid, same skeleton setup as the mannequin so your animations just work on them. that's the whole point of the swapping thing right?

**Priya Natarajan** · 15:12
right 👍

**Priya Natarajan** · 15:20
2D stuff: select screen portraits, store art, prompt icons

**Owen Calder** · 15:26
portraits: bust shots, 3/4 view, painted, plain warm background, same lighting on all three. they HAVE to match the 3D models. nothing kills a select screen faster than the portrait and the model looking like two different people

**Owen Calder** · 15:28
store icon: the knight's helmet as a silhouette or near-silhouette, on teal. no text (unity rule anyway)

**Owen Calder** · 15:29
key art: knight holding the relic up, 3/4 view, warm stone archway behind her, relic glow lighting her visor from underneath. scout and heavy can be in the background on the cover if it fits. social crop is just knight + relic

**Owen Calder** · 15:31
prompt icons: simple flat glyphs, white on a dark rounded square, slight bevel. our own style, don't copy anyone's official button art. keyboard + xbox-style + playstation-style face buttons at least

**Priya Natarajan** · 15:33
and the promo video?

**Owen Calder** · 15:40
30s, cut to something upbeat. rough beats: select screen → each character doing their thing (scout double air-dash, heavy lifting the big crate, knight opening a door) → relic pickup → logo. I can storyboard it properly when I'm back if that helps

**Priya Natarajan** · 15:41
when are you back again?

**Owen Calder** · 15:41
out mon 16th to fri 20th, back mon 23rd

**Priya Natarajan** · 15:42
assets are due the 24th 😬

**Owen Calder** · 15:44
yeah... ok. I can do rough turnaround sheets for the three characters by thursday so the generators have something to match. front/side/back, flat colour, nothing fancy. they won't be pretty

**Priya Natarajan** · 15:46
fwiw the pipeline can do concept art too, if that saves you time

**Owen Calder** · 15:51
hm. if it does the turnarounds I'd want to approve them before anything goes to 3D. but I'm out exactly when that'd happen. ugh

**Owen Calder** · 15:52
you approve them? you know what I like by now

**Priya Natarajan** · 15:52
I am not an artist lol

**Owen Calder** · 15:53
you've sat through every crit I've ever given, you'll be fine. if something feels off it probably is. trust the silhouette rule

**Priya Natarajan** · 15:54
ok. I'll leave it as TBD on the doc and we'll sort it before thursday

**Owen Calder** · 15:58
one more thing, whatever you give it, give it the hex codes. every time someone paraphrases my colours they come out muddy

**Priya Natarajan** · 15:58
will do. thank you, this is great. so... approved?

**Owen Calder** · 15:59
approved with all of the above. put "with comments" and link this

**Owen Calder** · 16:01
👍 and tell jess the relic is SMALL. I know she wants it big in the key art. it can glow big. it's small
