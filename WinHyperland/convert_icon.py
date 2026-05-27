from PIL import Image

# Open the generated image
img = Image.open(r"C:\Users\User\.gemini\antigravity\brain\abf73175-dfee-45b6-986f-14edf21a6bd3\artifacts\winhyperisland_logo.png")

# Resize to square
img = img.resize((256, 256), Image.Resampling.LANCZOS)

# Save as ICO with multiple sizes for Windows support
icon_sizes = [(16,16), (32, 32), (48, 48), (64,64), (128, 128), (256, 256)]
img.save(r"d:\Antigravity\WinHyperland\logo.ico", format="ICO", sizes=icon_sizes)

print("Icon successfully created and saved.")
