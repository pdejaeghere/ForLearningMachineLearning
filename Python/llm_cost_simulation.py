# llm_cost_simulation.py

from tiktoken import encoding_for_model
import json
import argparse
from typing import Dict, List

# -------------------------
# FONCTION TOKEN COUNT
# -------------------------
def count_tokens_openai(model: str,text: str,  show_decoded: bool = False) -> int:
    """Compte le nombre de tokens pour un texte donné et un modèle.
    Si `show_decoded` est True, affiche pour chaque token son id et sa
    représentation décodée (sous-chaîne)."""
    enc = encoding_for_model(model)
    encoded = enc.encode(text)
    tokens="";
    if show_decoded:              
        print(f"Texte : {text}")
        print(f"Nombres de mots : {len(text.split())}")
        print(f"Tokens détaillés: nombres {len(encoded)}")
        for i, t in enumerate(encoded):
            try:
                s = enc.decode([t])
            except Exception:
                s = "<impossible de décoder>"
            tokens += s+ "|"
            
        print(tokens.strip())
        print ("-" * 40)
    

    return len(encoded)


def simulate_chat_cost(
    model_name: str,
    messages: List[Dict],
    input_price_per_million: float,
    output_price_per_million: float
):
    """
    Simule le coût réel cumulatif d'un chat API.
    Chaque message 'user' déclenche une nouvelle requête.
    """

    history = []
    total_input_tokens = 0
    total_output_tokens = 0
    total_cost = 0.0
    total_pingpong = 0
    for msg in messages:

        history.append(msg)
        
        role = msg["Role"]["Label"].lower()

        # Si message user → nouvelle requête
        if role == "user":

            # Input = tout l'historique jusqu'à ce user
            input_text = json.dumps(history, separators=(',', ':'))
            input_tokens = count_tokens_openai(model_name, input_text)
            total_pingpong += 1
            total_input_tokens += input_tokens

        # Si message assistant → réponse générée
        if role == "assistant":

            output_text = json.dumps(msg["Items"], separators=(',', ':'))
            output_tokens = count_tokens_openai(model_name, output_text)

            total_output_tokens += output_tokens

    total_cost = (
        (total_input_tokens / 1_000_000) * input_price_per_million +
        (total_output_tokens / 1_000_000) * output_price_per_million
    )

    return {
        "total_input_tokens": total_input_tokens,
        "total_output_tokens": total_output_tokens,
        "total_tokens": total_input_tokens + total_output_tokens,
        "total_pingpong":total_pingpong,
        "estimated_cost": round(total_cost, 6)
    }




parser = argparse.ArgumentParser()
parser.add_argument("--model", required=True)
parser.add_argument("--input_price", type=float, required=True)
parser.add_argument("--output_price", type=float, required=True)
parser.add_argument("--historic_file", required=True)

args = parser.parse_args()

with open(args.historic_file, "r", encoding="utf-8-sig") as f:
    data = json.load(f)

result = simulate_chat_cost(
    model_name=args.model,
    messages=data["History"],
    input_price_per_million=args.input_price,
    output_price_per_million=args.output_price
)

print("\n=== Résultat Simulation ===")
print(result)