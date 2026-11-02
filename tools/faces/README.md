# Faces and crests (Qwen-Image in ComfyUI)

A player's portrait is **made once, when he is created**, and stays with him for good; he ages by showing the portrait of his age.
Nothing is generated while a match is played. A club's crest is made from a sentence its owner writes.

## What is here

| | |
|---|---|
| `PlayerAppearance` (engine) | The data a face is made from: look (African, Mediterranean...), skin 1-6, hairstyle, hair colour, facial hair, build, a seed. Decided at creation by where the player comes from; plays no part in the match. In the replay header as `look`. |
| `prompts.py` | From the look and an age to the text of a portrait: hair greys, lines and wrinkles come with age; no shirt in the picture. |
| `comfy.py`, `workflows/` | A client for ComfyUI's HTTP API and two workflows (text to image; image edit) in API format with `{{placeholders}}`. |
| `generate_faces.py` | A roster to portraits, one per age, and `manifest.json` for the page. Resumable. |
| `generate_logo.py`, `server.py` | A crest from a prompt. `server.py` is a small local server the page calls (POST `/logo`, POST `/face`). |
| the page (`tools/demo-page/js/19a-faces.js`) | Shows the portrait of the player's age over a **vector shirt in the team's colours** (so a colour change is instant), or - with no portrait yet - a vector face drawn from the same look data, aged by his years. The crest: "Club crest" in the user's kit screen. |

## Setting it up

1. **ComfyUI** (https://github.com/comfyanonymous/ComfyUI), with a GPU. Qwen-Image is big; ComfyUI's Qwen-Image page lists the memory
   it needs and the smaller (fp8) files used here.
2. Put the model files in ComfyUI: `models/diffusion_models/qwen_image_fp8_e4m3fn.safetensors`,
   `models/text_encoders/qwen_2.5_vl_7b_fp8_scaled.safetensors`, `models/vae/qwen_image_vae.safetensors`
   (for ageing by editing: `qwen_image_edit_fp8_e4m3fn.safetensors` as well). Check the licence of the models you use.
3. `pip install pillow` (and, to cut the background out, `pip install rembg`).
4. **Check the workflows once.** `workflows/*.json` follow ComfyUI's Qwen-Image examples as I know them and **have not been run against a real
   ComfyUI** (this was built where there is no GPU; the pipeline around them is tested with a stand-in ComfyUI). In ComfyUI open the
   Qwen-Image template, run it, `Save (API format)`, and compare node names and inputs with `workflows/qwen_text2img.json` - fix the file if
   they differ. Same for `qwen_edit.json`.
5. Portraits for the demo's squads:
   ```
   cd sim && dotnet run -c Release --project SoccerVision.SimRunner -- --export-roster 20 > roster.json
   python3 tools/faces/generate_faces.py --roster roster.json --out docs/faces --ages 18,22,26,30,34,38
   ```
   (`--method edit` ages the first portrait with Qwen-Image-Edit - the same man, older - instead of generating each age.)
6. Crests: `python3 tools/faces/server.py` (ComfyUI running), then open the page in user mode; the address of the server is
   `http://127.0.0.1:8765`, or `#faces=<address>` at the end of the page's address.

## Tests

`python3 -m unittest tests.test_pipeline` (from `tools/faces`): prompts, workflow filling, the generation with a stand-in ComfyUI, the server.
