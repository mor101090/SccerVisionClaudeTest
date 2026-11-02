"""From a player's look (the `look` object of the replay header, see PlayerAppearance.cs) and an age to the text a portrait is made from."""

LOOK_TEXT = {
    "european": "a European man with fair skin",
    "eastern-european": "an Eastern European man with fair skin and strong cheekbones",
    "mediterranean": "a Mediterranean man with olive skin",
    "middle-eastern": "a Middle Eastern man with tan skin",
    "east-african": "an East African man with dark brown skin, fine features, as in Ethiopia",
    "west-african": "a West African man with dark skin",
    "latin-american": "a Latin American man with warm brown skin",
    "east-asian": "an East Asian man",
}
SKIN_TEXT = {1: "very fair", 2: "fair", 3: "light olive", 4: "tan", 5: "dark brown", 6: "deep dark brown"}
HAIR_TEXT = {
    "short": "short neat hair", "buzz": "a buzz cut", "wavy": "wavy hair of medium length", "curly": "short curly hair",
    "afro": "a rounded natural afro", "receding": "short hair with a receding hairline", "bald": "a shaved bald head", "long": "long hair tied back",
}
FACIAL_TEXT = {"none": "clean-shaven", "stubble": "light stubble", "short-beard": "a short trimmed beard", "full-beard": "a full beard", "moustache": "a moustache"}
BUILD_TEXT = {"slim": "slim", "athletic": "athletic", "stocky": "stocky, broad-shouldered"}


def hair_colour_at(look, age):
    """The hair greys with age."""
    c = look.get("hairColour", "dark-brown").replace("-", " ")
    if age >= 45: return "mostly grey " + c
    if age >= 36: return "salt-and-pepper " + c
    if age >= 31: return c + " with a few grey hairs"
    return c


def age_text(age):
    if age <= 19: return "a teenager of {} years".format(age)
    if age <= 24: return "a young man of about {}".format(age)
    if age <= 31: return "a man of about {}".format(age)
    if age <= 37: return "a man of about {} with faint lines on his face".format(age)
    return "a man of about {} with visible wrinkles and ageing skin".format(age)


# The shirt is drawn in a flat chroma green so that imaging.split_shirt can lift it out: the page then colours it in the team's kit
# (the folds and the light stay real, the colour follows the kit - no picture per kit).
SHIRT_TEXT = ("Wearing a plain solid bright chroma-green (#00B140) crew-neck football shirt with short sleeves, completely plain: "
              "no logos, no badge, no sponsor, no text, no stripes, no collar trim.")

NEGATIVE = ("cartoon, illustration, painting, 3d render, text, letters, watermark, logo, badge, crest, sponsor, brand, number, stripes, "
            "glasses, hat, headband, hands, multiple people, deformed face, asymmetric eyes, blurry, low quality, celebrity, famous person")


def portrait_prompt(look, age):
    """Ultra-realistic head-and-shoulders portrait in the style the user tested with Qwen-Image, with the player's look and age."""
    who = LOOK_TEXT.get(look.get("look"), "a man")
    return " ".join([
        "Ultra-realistic professional portrait of a male professional football (soccer) player: {}, {}.".format(who, age_text(age)),
        "{} skin.".format(SKIN_TEXT.get(look.get("skin", 3), "light olive")).capitalize(),
        "He has {} in {}, and is {}.".format(HAIR_TEXT.get(look.get("hair"), "short hair"), hair_colour_at(look, age), FACIAL_TEXT.get(look.get("facial"), "clean-shaven")),
        "{} build.".format(BUILD_TEXT.get(look.get("build"), "athletic").capitalize()),
        "Looking directly into the camera with a confident, focused expression.",
        "Medium close-up framed from the top of the head down to the upper chest, both shoulders visible, perfectly centered, eye-level camera, direct eye contact.",
        SHIRT_TEXT,
        "Highly detailed realistic facial features, natural skin texture, subtle imperfections, realistic hair, professional studio lighting, "
        "sharp focus on the eyes, 85mm portrait lens, shallow depth of field, clean dark neutral grey background, photorealistic.",
        "Head and shoulders only, no hands, no full body; the face fully visible and facing straight toward the camera.",
    ])


def age_edit_prompt(look, age):
    """For an image-edit model: the same man, only older (or younger)."""
    return ("Make this exact same man {}. Keep his identity, face shape, skin tone, expression, pose, framing and background unchanged; "
            "only change what age changes: hair colour {}, skin, lines.".format(age_text(age), hair_colour_at(look, age)))


def logo_prompt(user_prompt, team_name=None):
    """A team crest from the player's own words. Qwen-Image draws text well: the name goes in if given."""
    text = ' with the team name "{}" written on it'.format(team_name) if team_name else ""
    return ("A professional football club crest logo{}: {}. Flat vector style, bold clean shapes, limited palette, symmetrical shield or "
            "badge shape, centred, plain white background, no extra text, high quality graphic design.".format(text, user_prompt.strip()))
