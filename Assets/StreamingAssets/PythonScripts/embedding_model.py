from sentence_transformers import SentenceTransformer

model = SentenceTransformer("all-MiniLM-L6-v2")

def get_embedding(word):
    return model.encode([word])[0].tolist()

def get_embeddings(words):
    return model.encode(words).tolist()